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
using System.Collections.Specialized;
using Apache.NMS.Util;

namespace Apache.NMS.Stomp.Commands
{
    /// <summary>
    /// Summary description for Destination.
    /// </summary>
    public abstract class Destination : BaseDataStructure, IDestination
    {
        /// <summary>
        /// Topic Destination object
        /// </summary>
        public const int STOMP_TOPIC = 1;
        /// <summary>
        /// Temporary Topic Destination object
        /// </summary>
        public const int STOMP_TEMPORARY_TOPIC = 2;
        /// <summary>
        /// Queue Destination object
        /// </summary>
        public const int STOMP_QUEUE = 3;
        /// <summary>
        /// Temporary Queue Destination object
        /// </summary>
        public const int STOMP_TEMPORARY_QUEUE = 4;
        /// <summary>
        /// prefix for Advisory message destinations
        /// </summary>
        public const String ADVISORY_PREFIX = "ActiveMQ.Advisory.";
        /// <summary>
        /// prefix for consumer advisory destinations
        /// </summary>
        public const String CONSUMER_ADVISORY_PREFIX = ADVISORY_PREFIX + "Consumers.";
        /// <summary>
        /// prefix for producer advisory destinations
        /// </summary>
        public const String PRODUCER_ADVISORY_PREFIX = ADVISORY_PREFIX + "Producers.";
        /// <summary>
        /// prefix for connection advisory destinations
        /// </summary>
        public const String CONNECTION_ADVISORY_PREFIX = ADVISORY_PREFIX + "Connections.";

        /// <summary>
        /// The default target for ordered destinations
        /// </summary>
        public const String DEFAULT_ORDERED_TARGET = "coordinator";

        private const String TEMP_PREFIX = "{TD{";
        private const String TEMP_POSTFIX = "}TD}";
        private const String COMPOSITE_SEPARATOR = ",";

        private String physicalName = "";
        private StringDictionary options = null;

        // Cached transient data
        private bool exclusive;
        private bool ordered;
        private bool advisory;
        private String orderedTarget = DEFAULT_ORDERED_TARGET;

        /// <summary>
        /// The Default Constructor
        /// </summary>
        protected Destination()
        {
        }

        /// <summary>
        /// Construct the Destination with a defined physical name;
        /// </summary>
        /// <param name="name"></param>
        protected Destination(String name)
        {
            setPhysicalName(name);
            //this.advisory = name != null && name.StartsWith(ADVISORY_PREFIX);
        }

        public bool IsTopic
        {
            get
            {
                int destinationType = GetDestinationType();
                return STOMP_TOPIC == destinationType
                    || STOMP_TEMPORARY_TOPIC == destinationType;
            }
        }

        public bool IsQueue
        {
            get
            {
                int destinationType = GetDestinationType();
                return STOMP_QUEUE == destinationType
                    || STOMP_TEMPORARY_QUEUE == destinationType;
            }
        }

        public bool IsTemporary
        {
            get
            {
                int destinationType = GetDestinationType();
                return STOMP_TEMPORARY_QUEUE == destinationType
                    || STOMP_TEMPORARY_TOPIC == destinationType;
            }
        }

        /// <summary>
        /// Dictionary of name/value pairs representing option values specified
        /// in the URI used to create this Destination.  A null value is returned
        /// if no options were specified.
        /// </summary>
        internal StringDictionary Options
        {
            get { return this.options; }
        }

        private void setPhysicalName(string name)
        {
            this.physicalName = name;

            int p = name.IndexOf('?');
            if(p >= 0)
            {
                String optstring = physicalName.Substring(p + 1);
                this.physicalName = name.Substring(0, p);
                options = URISupport.ParseQuery(optstring);
            }
        }

        /// <summary>
        /// </summary>
        /// <returns>Returns the advisory.</returns>
        public bool IsAdvisory()
        {
            return advisory;
        }

        /// <summary>
        /// </summary>
        /// <param name="advisory">The advisory to set.</param>
        public void SetAdvisory(bool advisory)
        {
            this.advisory = advisory;
        }

        /// <summary>
        /// </summary>
        /// <returns>true if this is a destination for Consumer advisories</returns>
        public bool IsConsumerAdvisory()
        {
            return IsAdvisory() && physicalName.StartsWith(CONSUMER_ADVISORY_PREFIX);
        }

        /// <summary>
        /// </summary>
        /// <returns>true if this is a destination for Producer advisories</returns>
        public bool IsProducerAdvisory()
        {
            return IsAdvisory() && physicalName.StartsWith(PRODUCER_ADVISORY_PREFIX);
        }

        /// <summary>
        /// </summary>
        /// <returns>true if this is a destination for Connection advisories</returns>
        public bool IsConnectionAdvisory()
        {
            return IsAdvisory() && physicalName.StartsWith(CONNECTION_ADVISORY_PREFIX);
        }

        /// <summary>
        /// </summary>
        /// <returns>Returns the exclusive.</returns>
        public bool IsExclusive()
        {
            return exclusive;
        }

        /// <summary>
        /// </summary>
        /// <param name="exclusive">The exclusive to set.</param>
        public void SetExclusive(bool exclusive)
        {
            this.exclusive = exclusive;
        }

        /// <summary>
        /// </summary>
        /// <returns>Returns the ordered.</returns>
        public bool IsOrdered()
        {
            return ordered;
        }

        /// <summary>
        /// </summary>
        /// <param name="ordered">The ordered to set.</param>
        public void SetOrdered(bool ordered)
        {
            this.ordered = ordered;
        }

        /// <summary>
        /// </summary>
        /// <returns>Returns the orderedTarget.</returns>
        public String GetOrderedTarget()
        {
            return orderedTarget;
        }

        /// <summary>
        /// </summary>
        /// <param name="orderedTarget">The orderedTarget to set.</param>
        public void SetOrderedTarget(String orderedTarget)
        {
            this.orderedTarget = orderedTarget;
        }

        /// <summary>
        /// A helper method to return a descriptive string for the topic or queue
        /// </summary>
        /// <param name="destination"></param>
        /// <returns>a descriptive string for this queue or topic</returns>
        public static String Inspect(Destination destination)
        {
            if(destination is ITopic)
            {
                return "Topic(" + destination.ToString() + ")";
            }
            else
            {
                return "Queue(" + destination.ToString() + ")";
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="destination"></param>
        /// <returns></returns>
        public static Destination Transform(IDestination destination)
        {
            Destination result = null;
            if(destination != null)
            {
                if(destination is Destination)
                {
                    result = (Destination) destination;
                }
                else
                {
                    if(destination is ITemporaryQueue)
                    {
                        result = new ActiveMQTempQueue(((IQueue) destination).QueueName);
                    }
                    else if(destination is ITemporaryTopic)
                    {
                        result = new ActiveMQTempTopic(((ITopic) destination).TopicName);
                    }
                    else if(destination is IQueue)
                    {
                        result = new ActiveMQQueue(((IQueue) destination).QueueName);
                    }
                    else if(destination is ITopic)
                    {
                        result = new ActiveMQTopic(((ITopic) destination).TopicName);
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Create a Destination
        /// </summary>
        /// <param name="type"></param>
        /// <param name="pyhsicalName"></param>
        /// <returns></returns>
        public static Destination CreateDestination(int type, String pyhsicalName)
        {
            Destination result = null;
            if(pyhsicalName == null)
            {
                return null;
            }
            else if(type == STOMP_TOPIC)
            {
                result = new Topic(pyhsicalName);
            }
            else if(type == STOMP_TEMPORARY_TOPIC)
            {
                result = new TempTopic(pyhsicalName);
            }
            else if(type == STOMP_QUEUE)
            {
                result = new Queue(pyhsicalName);
            }
            else
            {
                result = new TempQueue(pyhsicalName);
            }
            return result;
        }

        /// <summary>
        /// Create a temporary name from the clientId
        /// </summary>
        /// <param name="clientId"></param>
        /// <returns></returns>
        public static String CreateTemporaryName(String clientId)
        {
            return TEMP_PREFIX + clientId + TEMP_POSTFIX;
        }

        /// <summary>
        /// From a temporary destination find the clientId of the Connection that created it
        /// </summary>
        /// <param name="destination"></param>
        /// <returns>the clientId or null if not a temporary destination</returns>
        public static String GetClientId(Destination destination)
        {
            String answer = null;
            if(destination != null && destination.IsTemporary)
            {
                String name = destination.PhysicalName;
                int start = name.IndexOf(TEMP_PREFIX);
                if(start >= 0)
                {
                    start += TEMP_PREFIX.Length;
                    int stop = name.LastIndexOf(TEMP_POSTFIX);
                    if(stop > start && stop < name.Length)
                    {
                        answer = name.Substring(start, stop);
                    }
                }
            }
            return answer;
        }

        /// <summary>
        /// </summary>
        /// <param name="o">object to compare</param>
        /// <returns>1 if this is less than o else 0 if they are equal or -1 if this is less than o</returns>
        public int CompareTo(Object o)
        {
            if(o is Destination)
            {
                return CompareTo((Destination) o);
            }
            return -1;
        }

        /// <summary>
        /// Lets sort by name first then lets sort topics greater than queues
        /// </summary>
        /// <param name="that">another destination to compare against</param>
        /// <returns>1 if this is less than o else 0 if they are equal or -1 if this is less than o</returns>
        public int CompareTo(Destination that)
        {
            int answer = 0;
            if(physicalName != that.physicalName)
            {
                if(physicalName == null)
                {
                    return -1;
                }
                else if(that.physicalName == null)
                {
                    return 1;
                }
                answer = physicalName.CompareTo(that.physicalName);
            }

            if(answer == 0)
            {
                if(IsTopic)
                {
                    if(that.IsQueue)
                    {
                        return 1;
                    }
                }
                else
                {
                    if(that.IsTopic)
                    {
                        return -1;
                    }
                }
            }
            return answer;
        }

        /// <summary>
        /// </summary>
        /// <returns>Returns the Destination type</returns>
        public abstract int GetDestinationType();

        public String PhysicalName
        {
            get { return this.physicalName; }
            set
            {
                this.physicalName = value;
                this.advisory = (value != null && value.StartsWith(ADVISORY_PREFIX));
            }
        }

        /// <summary>
        /// Returns true if this destination represents a collection of
        /// destinations; allowing a set of destinations to be published to or subscribed
        /// from in one NMS operation.
        /// <p/>
        /// If this destination is a composite then you can call {@link #getChildDestinations()}
        /// to return the list of child destinations.
        /// </summary>
        public bool IsComposite
        {
            get
            {
                return physicalName.IndexOf(COMPOSITE_SEPARATOR) > 0;
            }
        }

        /*public List GetChildDestinations() {
         List answer = new ArrayList();
         StringTokenizer iter = new StringTokenizer(physicalName, COMPOSITE_SEPARATOR);
         while (iter.hasMoreTokens()) {
         String name = iter.nextToken();
         Destination child = null;
         if (name.StartsWith(QUEUE_PREFIX)) {
         child = new ActiveMQQueue(name.Substring(QUEUE_PREFIX.Length));
         }
         else if (name.StartsWith(TOPIC_PREFIX)) {
         child = new ActiveMQTopic(name.Substring(TOPIC_PREFIX.Length));
         }
         else {
         child = createDestination(name);
         }
         answer.add(child);
         }
         if (answer.size() == 1) {
         // lets put ourselves inside the collection
         // as we are not really a composite destination
         answer.set(0, this);
         }
         return answer;
         }*/

        /// <summary>
        /// </summary>
        /// <returns>string representation of this instance</returns>
        public override String ToString()
        {
            switch(DestinationType)
            {
            case DestinationType.Topic:
            return "topic://" + PhysicalName;

            case DestinationType.TemporaryTopic:
            return "temp-topic://" + PhysicalName;

            case DestinationType.TemporaryQueue:
            return "temp-queue://" + PhysicalName;

            default:
            return "queue://" + PhysicalName;
            }
        }

        /// <summary>
        /// </summary>
        /// <returns>hashCode for this instance</returns>
        public override int GetHashCode()
        {
            int answer = 37;

            if(this.physicalName != null)
            {
                answer = physicalName.GetHashCode();
            }
            if(IsTopic)
            {
                answer ^= 0xfabfab;
            }
            return answer;
        }

        /// <summary>
        /// if the object passed in is equivalent, return true
        /// </summary>
        /// <param name="obj">the object to compare</param>
        /// <returns>true if this instance and obj are equivalent</returns>
        public override bool Equals(Object obj)
        {
            bool result = this == obj;
            if(!result && obj != null && obj is Destination)
            {
                Destination other = (Destination) obj;
                result = this.GetDestinationType() == other.GetDestinationType()
                    && this.physicalName.Equals(other.physicalName);
            }
            return result;
        }

        /// <summary>
        /// </summary>
        /// <returns>true if the destination matches multiple possible destinations</returns>
        public bool IsWildcard()
        {
            if(physicalName != null)
            {
                return physicalName.IndexOf(DestinationFilter.ANY_CHILD) >= 0
                    || physicalName.IndexOf(DestinationFilter.ANY_DESCENDENT) >= 0;
            }
            return false;
        }

        /// <summary>
        /// Factory method to create a child destination if this destination is a composite
        /// </summary>
        /// <param name="name"></param>
        /// <returns>the created Destination</returns>
        public abstract Destination CreateDestination(String name);


        public abstract DestinationType DestinationType
        {
            get;
        }

        public override Object Clone()
        {
            // Since we are a derived class use the base's Clone()
            // to perform the shallow copy. Since it is shallow it
            // will include our derived class. Since we are derived,
            // this method is an override.
            Destination o = (Destination) base.Clone();

            // Now do the deep work required
            // If any new variables are added then this routine will
            // likely need updating

            return o;
        }
    }
}

