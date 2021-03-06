/*
 * Licensed to the Apache Software Foundation (ASF) under one or more
 * contributor license agreements.  See the NOTICE file distributed with
 * this work for additional information regarding copyright ownership.
 * The ASF licenses this file to You under the Apache License, Version 2.0
 * (the "License"); you may not use this file except in compliance with
 * the License.  You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Collections;
using System.Threading;
using Apache.NMS.Util;
using Org.Apache.Qpid.Messaging;

// Typedef for options map
using OptionsMap = System.Collections.Generic.Dictionary<System.String, System.Object>;

namespace Apache.NMS.Amqp
{
    /// <summary>
    /// Amqp provider of ISession
    /// </summary>
    public class Session : ISession, IStartable, IStoppable
    {
        /// <summary>
        /// Private object used for synchronization, instead of public "this"
        /// </summary>
        private readonly object myLock = new object();

        private readonly IDictionary consumers = Hashtable.Synchronized(new Hashtable());
        private readonly IDictionary producers = Hashtable.Synchronized(new Hashtable());

        private Connection connection;
        private AcknowledgementMode acknowledgementMode;
        private IMessageConverter messageConverter;
        private readonly int id;

        private int consumerCounter;
        private int producerCounter;
        private long nextDeliveryId;
        private long lastDeliveredSequenceId;
        private readonly object sessionLock = new object();
        private readonly Atomic<bool> started = new Atomic<bool>(false);
        protected bool disposed = false;
        protected bool closed = false;
        protected bool closing = false;
        private TimeSpan disposeStopTimeout = TimeSpan.FromMilliseconds(30000);
        private TimeSpan closeStopTimeout = TimeSpan.FromMilliseconds(Timeout.Infinite);
        private TimeSpan requestTimeout;

        private Org.Apache.Qpid.Messaging.Session qpidSession = null; // Don't create until Start()

        public Session(Connection connection, int sessionId, AcknowledgementMode acknowledgementMode)
        {
            this.connection = connection;
            this.acknowledgementMode = acknowledgementMode;
            MessageConverter = connection.MessageConverter;
            id = sessionId;
            if (this.acknowledgementMode == AcknowledgementMode.Transactional)
            {
                // TODO: transactions
                throw new NotSupportedException("Transactions are not supported by Qpid/Amqp");
            }
            else if (acknowledgementMode == AcknowledgementMode.DupsOkAcknowledge)
            {
                this.acknowledgementMode = AcknowledgementMode.AutoAcknowledge;
            }
            if (connection.IsStarted)
            {
                this.Start();
            }
            connection.AddSession(this);
        }

        public AcknowledgementMode AcknowledgementMode
        {
            get { return this.acknowledgementMode; }
        }

        public bool IsClientAcknowledge
        {
            get { return this.acknowledgementMode == AcknowledgementMode.ClientAcknowledge; }
        }

        public bool IsAutoAcknowledge
        {
            get { return this.acknowledgementMode == AcknowledgementMode.AutoAcknowledge; }
        }

        public bool IsDupsOkAcknowledge
        {
            get { return this.acknowledgementMode == AcknowledgementMode.DupsOkAcknowledge; }
        }

        public bool IsIndividualAcknowledge
        {
            get { return this.acknowledgementMode == AcknowledgementMode.IndividualAcknowledge; }
        }

        public bool IsTransacted
        {
            get { return this.acknowledgementMode == AcknowledgementMode.Transactional; }
        }

        #region IStartable Methods
        /// <summary>
        /// Create new unmanaged session and start senders and receivers
        /// Associated connection must be open.
        /// </summary>
        public void Start()
        {
            // Don't try creating session if connection not yet up
            if (!connection.IsStarted)
            {
                throw new ConnectionClosedException();
            }

            if (started.CompareAndSet(false, true))
            {
                try
                {
                    // Create qpid session
                    if (qpidSession == null)
                    {
                        qpidSession = connection.CreateQpidSession();
                    }

                    // Start producers and consumers
                    lock (producers.SyncRoot)
                    {
                        foreach (MessageProducer producer in producers.Values)
                        {
                            producer.Start();
                        }
                    }
                    lock (consumers.SyncRoot)
                    {
                        foreach (MessageConsumer consumer in consumers.Values)
                        {
                            consumer.Start();
                        }
                    }
                }
                catch (Org.Apache.Qpid.Messaging.QpidException e)
                {
                    throw new SessionClosedException( "Failed to create session : " + e.Message );
                }
            }
        }

        public bool IsStarted
        {
            get { return started.Value; }
        }
        #endregion

        #region IStoppable Methods
        public void Stop()
        {
            if (started.CompareAndSet(true, false))
            {
                try
                {
                    lock (producers.SyncRoot)
                    {
                        foreach (MessageProducer producer in producers.Values)
                        {
                            producer.Stop();
                        }
                    }
                    lock (consumers.SyncRoot)
                    {
                        foreach (MessageConsumer consumer in consumers.Values)
                        {
                            consumer.Stop();
                        }
                    }

                    qpidSession.Dispose();
                    qpidSession = null;
                }
                catch (Org.Apache.Qpid.Messaging.QpidException e)
                {
                    throw new NMSException("Failed to close session with Id " + SessionId.ToString() + " : " + e.Message);
                }
            }
        }
        #endregion

        #region IDisposable Methods
        public void Dispose()
        {
            Dispose(true);
        }
        #endregion


        protected void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }

            try
            {
                // Force a Stop when we are Disposing vs a Normal Close.
                Close();
            }
            catch
            {
                // Ignore network errors.
            }

            this.disposed = true;
        }

        public virtual void Close()
        {
            if (!this.closed)
            {
                try
                {
                    Tracer.InfoFormat("Closing The Session with Id {0}", SessionId);
                    DoClose();
                    Tracer.InfoFormat("Closed The Session with Id {0}", SessionId);
                }
                catch (Exception ex)
                {
                    Tracer.ErrorFormat("Error closing Session with id {0} : {1}", SessionId, ex);
                }
            }
        }

        internal void DoClose()
        {
            Shutdown();
        }

        internal void Shutdown()
        {
            //Tracer.InfoFormat("Executing Shutdown on Session with Id {0}", this.info.SessionId);

            if (this.closed)
            {
                return;
            }

            lock (myLock)
            {
                if (this.closed || this.closing)
                {
                    return;
                }

                try
                {
                    this.closing = true;

                    // Stop all message deliveries from this Session
                    lock (consumers.SyncRoot)
                    {
                        foreach (MessageConsumer consumer in consumers.Values)
                        {
                            consumer.Close();
                        }
                    }
                    consumers.Clear();

                    lock (producers.SyncRoot)
                    {
                        foreach (MessageProducer producer in producers.Values)
                        {
                            producer.Close();
                        }
                    }
                    producers.Clear();

                    Connection.RemoveSession(this);
                }
                catch (Exception ex)
                {
                    Tracer.ErrorFormat("Error closing Session with Id {0} : {1}", SessionId, ex);
                }
                finally
                {
                    this.closed = true;
                    this.closing = false;
                }
            }
        }

        public IMessageProducer CreateProducer()
        {
            return CreateProducer(null);
        }

        public IMessageProducer CreateProducer(IDestination destination)
        {
            if (destination == null)
            {
                throw new InvalidDestinationException("Cannot create a Consumer with a Null destination");
            }
            MessageProducer producer = null;
            try
            {
                Queue queue = new Queue(destination.ToString());
                producer = DoCreateMessageProducer(queue);

                this.AddProducer(producer);
            }
            catch (Exception)
            {
                if (producer != null)
                {
                    this.RemoveProducer(producer.ProducerId);
                    producer.Close();
                }

                throw;
            }

            return producer;
        }

        internal virtual MessageProducer DoCreateMessageProducer(Destination destination)
        {
            return new MessageProducer(this, GetNextProducerId(), destination);
        }

        public IMessageConsumer CreateConsumer(IDestination destination)
        {
            return CreateConsumer(destination, null, false);
        }

        public IMessageConsumer CreateConsumer(IDestination destination, string selector)
        {
            return CreateConsumer(destination, selector, false);
        }

        public IMessageConsumer CreateConsumer(IDestination destination, string selector, bool noLocal)
        {
            if (destination == null)
            {
                throw new InvalidDestinationException("Cannot create a Consumer with a Null destination");
            }

            MessageConsumer consumer = null;

            try
            {
                Queue queue = new Queue(destination.ToString());
                consumer = DoCreateMessageConsumer(GetNextConsumerId(), queue, acknowledgementMode);

                consumer.ConsumerTransformer = this.ConsumerTransformer;

                this.AddConsumer(consumer);

                if (this.Connection.IsStarted)
                {
                    consumer.Start();
                }
            }
            catch (Exception)
            {
                if (consumer != null)
                {
                    this.RemoveConsumer(consumer);
                    consumer.Close();
                }

                throw;
            }

            return consumer;
        }


        public IMessageConsumer CreateDurableConsumer(ITopic destination, string name, string selector, bool noLocal)
        {
            throw new NotSupportedException("TODO: Durable Consumer");
        }

        internal virtual MessageConsumer DoCreateMessageConsumer(int id, Destination destination, AcknowledgementMode mode)
        {
            return new MessageConsumer(this, id, destination, mode);
        }

        public void DeleteDurableConsumer(string name)
        {
            throw new NotSupportedException("TODO: Durable Consumer");
        }

        public IQueueBrowser CreateBrowser(IQueue queue)
        {
            throw new NotImplementedException();
        }

        public IQueueBrowser CreateBrowser(IQueue queue, string selector)
        {
            throw new NotImplementedException();
        }

        public IQueue GetQueue(string name)
        {
            return new Queue(name);
        }

        public ITopic GetTopic(string name)
        {
            return new Topic(name);
        }

        public IQueue GetQueue(string name, string subject, OptionsMap options)
        {
            return new Queue(name, subject, options);
        }

        public ITopic GetTopic(string name, string subject, OptionsMap options)
        {
            return new Topic(name, subject, options);
        }

        public ITemporaryQueue CreateTemporaryQueue()
        {
            throw new NotSupportedException("TODO: Temp queue");
        }

        public ITemporaryTopic CreateTemporaryTopic()
        {
            throw new NotSupportedException("TODO: Temp topic");
        }

        /// <summary>
        /// Delete a destination (Queue, Topic, Temp Queue, Temp Topic).
        /// </summary>
        public void DeleteDestination(IDestination destination)
        {
            // TODO: Implement if possible.  If not possible, then change exception to NotSupportedException().
            throw new NotImplementedException();
        }

        public IMessage CreateMessage()
        {
            BaseMessage answer = new BaseMessage();
            return answer;
        }


        public ITextMessage CreateTextMessage()
        {
            TextMessage answer = new TextMessage();
            return answer;
        }

        public ITextMessage CreateTextMessage(string text)
        {
            TextMessage answer = new TextMessage(text);
            return answer;
        }

        public IMapMessage CreateMapMessage()
        {
            return new MapMessage();
        }

        public IBytesMessage CreateBytesMessage()
        {
            return new BytesMessage();
        }

        public IBytesMessage CreateBytesMessage(byte[] body)
        {
            BytesMessage answer = new BytesMessage();
            answer.Content = body;
            return answer;
        }

        public IStreamMessage CreateStreamMessage()
        {
            return new StreamMessage();
        }

        public IObjectMessage CreateObjectMessage(Object body)
        {
            ObjectMessage answer = new ObjectMessage();
            answer.Body = body;
            return answer;
        }

        public void Commit()
        {
            throw new NotSupportedException("Transactions not supported by Qpid/Amqp");
        }

        public void Rollback()
        {
            throw new NotSupportedException("Transactions not supported by Qpid/Amqp");
        }

        public void Recover()
        {
            throw new NotSupportedException("Transactions not supported by Qpid/Amqp");
        }

        // Properties
        public Connection Connection
        {
            get { return connection; }
        }

        /// <summary>
        /// The default timeout for network requests.
        /// </summary>
        public TimeSpan RequestTimeout
        {
            get { return NMSConstants.defaultRequestTimeout; }
            set { }
        }

        public IMessageConverter MessageConverter
        {
            get { return messageConverter; }
            set { messageConverter = value; }
        }

        public bool Transacted
        {
            get { return acknowledgementMode == AcknowledgementMode.Transactional; }
        }

        private ConsumerTransformerDelegate consumerTransformer;
        public ConsumerTransformerDelegate ConsumerTransformer
        {
            get { return this.consumerTransformer; }
            set { this.consumerTransformer = value; }
        }

        private ProducerTransformerDelegate producerTransformer;
        public ProducerTransformerDelegate ProducerTransformer
        {
            get { return this.producerTransformer; }
            set { this.producerTransformer = value; }
        }

        public void AddConsumer(MessageConsumer consumer)
        {
            if (!this.closing)
            {
                // Registered with Connection before we register at the broker.
                consumers[consumer.ConsumerId] = consumer;
            }
        }

        public void RemoveConsumer(MessageConsumer consumer)
        {
            if (!this.closing)
            {
                consumers.Remove(consumer.ConsumerId);
            }
        }

        public void AddProducer(MessageProducer producer)
        {
            if (!this.closing)
            {
                this.producers[producer.ProducerId] = producer;
            }
        }

        public void RemoveProducer(int objectId)
        {
            if (!this.closing)
            {
                producers.Remove(objectId);
            }
        }

        public int GetNextConsumerId()
        {
            return Interlocked.Increment(ref consumerCounter);
        }

        public int GetNextProducerId()
        {
            return Interlocked.Increment(ref producerCounter);
        }

        public int SessionId
        {
            get { return id; }
        }


        public Org.Apache.Qpid.Messaging.Receiver CreateQpidReceiver(Address address)
        {
            if (!IsStarted)
            {
                throw new SessionClosedException();
            }
            return qpidSession.CreateReceiver(address);
        }

        public Org.Apache.Qpid.Messaging.Sender CreateQpidSender(Address address)
        {
            if (!IsStarted)
            {
                throw new SessionClosedException();
            }
            return qpidSession.CreateSender(address);
        }

        //
        // Acknowledges all outstanding messages that have been received
        // by the application on this session.
        // 
        // @param sync if true, blocks until the acknowledgement has been
        // processed by the server
        //
        public void Acknowledge()
        {
            qpidSession.Acknowledge(false);
        }

        public void Acknowledge(bool sync)
        {
            qpidSession.Acknowledge(sync);
        }

        //
        // These flavors of acknowledge are available in the qpid messaging
        // interface but not exposed to the NMS message/session stack.
        //
        // Acknowledges the specified message.
        //
        // void acknowledge(Message&, bool sync=false);
        //
        // Acknowledges all message up to the specified message.
        //
        // void acknowledgeUpTo(Message&, bool sync=false);

        #region Transaction State Events

        public event SessionTxEventDelegate TransactionStartedListener;
        public event SessionTxEventDelegate TransactionCommittedListener;
        public event SessionTxEventDelegate TransactionRolledBackListener;

        #endregion

    }
}
