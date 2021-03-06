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
 *  Marshaler code for OpenWire format for ConnectionInfo
 *
 *  NOTE!: This file is auto generated - do not modify!
 *         if you need to make a change, please see the Java Classes
 *         in the nms-activemq-openwire-generator module
 *
 */

using System;
using System.IO;

using Apache.NMS.ActiveMQ.Commands;

namespace Apache.NMS.ActiveMQ.OpenWire.V6
{
    /// <summary>
    ///  Marshalling code for Open Wire Format for ConnectionInfo
    /// </summary>
    class ConnectionInfoMarshaller : BaseCommandMarshaller
    {
        /// <summery>
        ///  Creates an instance of the Object that this marshaller handles.
        /// </summery>
        public override DataStructure CreateObject() 
        {
            return new ConnectionInfo();
        }

        /// <summery>
        ///  Returns the type code for the Object that this Marshaller handles..
        /// </summery>
        public override byte GetDataStructureType() 
        {
            return ConnectionInfo.ID_CONNECTIONINFO;
        }

        // 
        // Un-marshal an object instance from the data input stream
        // 
        public override void TightUnmarshal(OpenWireFormat wireFormat, Object o, BinaryReader dataIn, BooleanStream bs) 
        {
            base.TightUnmarshal(wireFormat, o, dataIn, bs);

            ConnectionInfo info = (ConnectionInfo)o;
            info.ConnectionId = (ConnectionId) TightUnmarshalCachedObject(wireFormat, dataIn, bs);
            info.ClientId = TightUnmarshalString(dataIn, bs);
            info.Password = TightUnmarshalString(dataIn, bs);
            info.UserName = TightUnmarshalString(dataIn, bs);

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
            info.BrokerMasterConnector = bs.ReadBoolean();
            info.Manageable = bs.ReadBoolean();
            info.ClientMaster = bs.ReadBoolean();
            info.FaultTolerant = bs.ReadBoolean();
            info.FailoverReconnect = bs.ReadBoolean();
        }

        //
        // Write the booleans that this object uses to a BooleanStream
        //
        public override int TightMarshal1(OpenWireFormat wireFormat, Object o, BooleanStream bs)
        {
            ConnectionInfo info = (ConnectionInfo)o;

            int rc = base.TightMarshal1(wireFormat, o, bs);
            rc += TightMarshalCachedObject1(wireFormat, (DataStructure)info.ConnectionId, bs);
            rc += TightMarshalString1(info.ClientId, bs);
            rc += TightMarshalString1(info.Password, bs);
            rc += TightMarshalString1(info.UserName, bs);
            rc += TightMarshalObjectArray1(wireFormat, info.BrokerPath, bs);
            bs.WriteBoolean(info.BrokerMasterConnector);
            bs.WriteBoolean(info.Manageable);
            bs.WriteBoolean(info.ClientMaster);
            bs.WriteBoolean(info.FaultTolerant);
            bs.WriteBoolean(info.FailoverReconnect);

            return rc + 0;
        }

        // 
        // Write a object instance to data output stream
        //
        public override void TightMarshal2(OpenWireFormat wireFormat, Object o, BinaryWriter dataOut, BooleanStream bs)
        {
            base.TightMarshal2(wireFormat, o, dataOut, bs);

            ConnectionInfo info = (ConnectionInfo)o;
            TightMarshalCachedObject2(wireFormat, (DataStructure)info.ConnectionId, dataOut, bs);
            TightMarshalString2(info.ClientId, dataOut, bs);
            TightMarshalString2(info.Password, dataOut, bs);
            TightMarshalString2(info.UserName, dataOut, bs);
            TightMarshalObjectArray2(wireFormat, info.BrokerPath, dataOut, bs);
            bs.ReadBoolean();
            bs.ReadBoolean();
            bs.ReadBoolean();
            bs.ReadBoolean();
            bs.ReadBoolean();
        }

        // 
        // Un-marshal an object instance from the data input stream
        // 
        public override void LooseUnmarshal(OpenWireFormat wireFormat, Object o, BinaryReader dataIn) 
        {
            base.LooseUnmarshal(wireFormat, o, dataIn);

            ConnectionInfo info = (ConnectionInfo)o;
            info.ConnectionId = (ConnectionId) LooseUnmarshalCachedObject(wireFormat, dataIn);
            info.ClientId = LooseUnmarshalString(dataIn);
            info.Password = LooseUnmarshalString(dataIn);
            info.UserName = LooseUnmarshalString(dataIn);

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
            info.BrokerMasterConnector = dataIn.ReadBoolean();
            info.Manageable = dataIn.ReadBoolean();
            info.ClientMaster = dataIn.ReadBoolean();
            info.FaultTolerant = dataIn.ReadBoolean();
            info.FailoverReconnect = dataIn.ReadBoolean();
        }

        // 
        // Write a object instance to data output stream
        //
        public override void LooseMarshal(OpenWireFormat wireFormat, Object o, BinaryWriter dataOut)
        {

            ConnectionInfo info = (ConnectionInfo)o;

            base.LooseMarshal(wireFormat, o, dataOut);
            LooseMarshalCachedObject(wireFormat, (DataStructure)info.ConnectionId, dataOut);
            LooseMarshalString(info.ClientId, dataOut);
            LooseMarshalString(info.Password, dataOut);
            LooseMarshalString(info.UserName, dataOut);
            LooseMarshalObjectArray(wireFormat, info.BrokerPath, dataOut);
            dataOut.Write(info.BrokerMasterConnector);
            dataOut.Write(info.Manageable);
            dataOut.Write(info.ClientMaster);
            dataOut.Write(info.FaultTolerant);
            dataOut.Write(info.FailoverReconnect);
        }
    }
}
