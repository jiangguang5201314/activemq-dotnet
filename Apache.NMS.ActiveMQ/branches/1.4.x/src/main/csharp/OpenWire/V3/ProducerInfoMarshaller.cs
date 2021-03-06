/*
 * Licensed to the Apache Software Foundation (ASF) under one or more
 * contributor license agreements.  See the NOTICE file distributed with
 * this work for additional information regarding copyright ownership.
 * The ASF licenses this file to You under the Apache License, Version 2.0
 * (the "License"); you may not use this file except in compliance with
 * the License.  You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

/*
 *
 *  Marshaler code for OpenWire format for ProducerInfo
 *
 *  NOTE!: This file is auto generated - do not modify!
 *         if you need to make a change, please see the Java Classes
 *         in the nms-activemq-openwire-generator module
 *
 */

using System;
using System.IO;

using Apache.NMS.ActiveMQ.Commands;

namespace Apache.NMS.ActiveMQ.OpenWire.V3
{
    /// <summary>
    ///  Marshalling code for Open Wire Format for ProducerInfo
    /// </summary>
    class ProducerInfoMarshaller : BaseCommandMarshaller
    {
        /// <summery>
        ///  Creates an instance of the Object that this marshaller handles.
        /// </summery>
        public override DataStructure CreateObject() 
        {
            return new ProducerInfo();
        }

        /// <summery>
        ///  Returns the type code for the Object that this Marshaller handles..
        /// </summery>
        public override byte GetDataStructureType() 
        {
            return ProducerInfo.ID_PRODUCERINFO;
        }

        // 
        // Un-marshal an object instance from the data input stream
        // 
        public override void TightUnmarshal(OpenWireFormat wireFormat, Object o, BinaryReader dataIn, BooleanStream bs) 
        {
            base.TightUnmarshal(wireFormat, o, dataIn, bs);

            ProducerInfo info = (ProducerInfo)o;
            info.ProducerId = (ProducerId) TightUnmarshalCachedObject(wireFormat, dataIn, bs);
            info.Destination = (ActiveMQDestination) TightUnmarshalCachedObject(wireFormat, dataIn, bs);

            if (bs.ReadBoolean()) {
                short size = dataIn.ReadInt16();
                BrokerId[] value = new BrokerId[size];
                for( int i=0; i < size; i++ ) {
                    value[i] = (BrokerId) TightUnmarshalNestedObject(wireFormat,dataIn, bs);
                }
                info.BrokerPath = value;
            }
            else {
                info.BrokerPath = null;
            }
            info.DispatchAsync = bs.ReadBoolean();
            info.WindowSize = dataIn.ReadInt32();
        }

        //
        // Write the booleans that this object uses to a BooleanStream
        //
        public override int TightMarshal1(OpenWireFormat wireFormat, Object o, BooleanStream bs)
        {
            ProducerInfo info = (ProducerInfo)o;

            int rc = base.TightMarshal1(wireFormat, o, bs);
            rc += TightMarshalCachedObject1(wireFormat, (DataStructure)info.ProducerId, bs);
            rc += TightMarshalCachedObject1(wireFormat, (DataStructure)info.Destination, bs);
            rc += TightMarshalObjectArray1(wireFormat, info.BrokerPath, bs);
            bs.WriteBoolean(info.DispatchAsync);

            return rc + 4;
        }

        // 
        // Write a object instance to data output stream
        //
        public override void TightMarshal2(OpenWireFormat wireFormat, Object o, BinaryWriter dataOut, BooleanStream bs)
        {
            base.TightMarshal2(wireFormat, o, dataOut, bs);

            ProducerInfo info = (ProducerInfo)o;
            TightMarshalCachedObject2(wireFormat, (DataStructure)info.ProducerId, dataOut, bs);
            TightMarshalCachedObject2(wireFormat, (DataStructure)info.Destination, dataOut, bs);
            TightMarshalObjectArray2(wireFormat, info.BrokerPath, dataOut, bs);
            bs.ReadBoolean();
            dataOut.Write(info.WindowSize);
        }

        // 
        // Un-marshal an object instance from the data input stream
        // 
        public override void LooseUnmarshal(OpenWireFormat wireFormat, Object o, BinaryReader dataIn) 
        {
            base.LooseUnmarshal(wireFormat, o, dataIn);

            ProducerInfo info = (ProducerInfo)o;
            info.ProducerId = (ProducerId) LooseUnmarshalCachedObject(wireFormat, dataIn);
            info.Destination = (ActiveMQDestination) LooseUnmarshalCachedObject(wireFormat, dataIn);

            if (dataIn.ReadBoolean()) {
                short size = dataIn.ReadInt16();
                BrokerId[] value = new BrokerId[size];
                for( int i=0; i < size; i++ ) {
                    value[i] = (BrokerId) LooseUnmarshalNestedObject(wireFormat,dataIn);
                }
                info.BrokerPath = value;
            }
            else {
                info.BrokerPath = null;
            }
            info.DispatchAsync = dataIn.ReadBoolean();
            info.WindowSize = dataIn.ReadInt32();
        }

        // 
        // Write a object instance to data output stream
        //
        public override void LooseMarshal(OpenWireFormat wireFormat, Object o, BinaryWriter dataOut)
        {

            ProducerInfo info = (ProducerInfo)o;

            base.LooseMarshal(wireFormat, o, dataOut);
            LooseMarshalCachedObject(wireFormat, (DataStructure)info.ProducerId, dataOut);
            LooseMarshalCachedObject(wireFormat, (DataStructure)info.Destination, dataOut);
            LooseMarshalObjectArray(wireFormat, info.BrokerPath, dataOut);
            dataOut.Write(info.DispatchAsync);
            dataOut.Write(info.WindowSize);
        }
    }
}
