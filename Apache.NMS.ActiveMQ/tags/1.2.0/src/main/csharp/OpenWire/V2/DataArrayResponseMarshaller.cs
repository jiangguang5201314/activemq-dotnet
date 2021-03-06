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
 *  Marshaler code for OpenWire format for DataArrayResponse
 *
 *  NOTE!: This file is auto generated - do not modify!
 *         if you need to make a change, please see the Java Classes
 *         in the nms-activemq-openwire-generator module
 *
 */

using System;
using System.Collections;
using System.IO;

using Apache.NMS.ActiveMQ.Commands;
using Apache.NMS.ActiveMQ.OpenWire;
using Apache.NMS.ActiveMQ.OpenWire.V2;

namespace Apache.NMS.ActiveMQ.OpenWire.V2
{
    /// <summary>
    ///  Marshalling code for Open Wire Format for DataArrayResponse
    /// </summary>
    class DataArrayResponseMarshaller : ResponseMarshaller
    {
        /// <summery>
        ///  Creates an instance of the Object that this marshaller handles.
        /// </summery>
        public override DataStructure CreateObject() 
        {
            return new DataArrayResponse();
        }

        /// <summery>
        ///  Returns the type code for the Object that this Marshaller handles..
        /// </summery>
        public override byte GetDataStructureType() 
        {
            return DataArrayResponse.ID_DATAARRAYRESPONSE;
        }

        // 
        // Un-marshal an object instance from the data input stream
        // 
        public override void TightUnmarshal(OpenWireFormat wireFormat, Object o, BinaryReader dataIn, BooleanStream bs) 
        {
            base.TightUnmarshal(wireFormat, o, dataIn, bs);

            DataArrayResponse info = (DataArrayResponse)o;

            if (bs.ReadBoolean()) {
                short size = dataIn.ReadInt16();
                DataStructure[] value = new DataStructure[size];
                for( int i=0; i < size; i++ ) {
                    value[i] = (DataStructure) TightUnmarshalNestedObject(wireFormat,dataIn, bs);
                }
                info.Data = value;
            }
            else {
                info.Data = null;
            }
        }

        //
        // Write the booleans that this object uses to a BooleanStream
        //
        public override int TightMarshal1(OpenWireFormat wireFormat, Object o, BooleanStream bs)
        {
            DataArrayResponse info = (DataArrayResponse)o;

            int rc = base.TightMarshal1(wireFormat, o, bs);
            rc += TightMarshalObjectArray1(wireFormat, info.Data, bs);

            return rc + 0;
        }

        // 
        // Write a object instance to data output stream
        //
        public override void TightMarshal2(OpenWireFormat wireFormat, Object o, BinaryWriter dataOut, BooleanStream bs)
        {
            base.TightMarshal2(wireFormat, o, dataOut, bs);

            DataArrayResponse info = (DataArrayResponse)o;
            TightMarshalObjectArray2(wireFormat, info.Data, dataOut, bs);
        }

        // 
        // Un-marshal an object instance from the data input stream
        // 
        public override void LooseUnmarshal(OpenWireFormat wireFormat, Object o, BinaryReader dataIn) 
        {
            base.LooseUnmarshal(wireFormat, o, dataIn);

            DataArrayResponse info = (DataArrayResponse)o;

            if (dataIn.ReadBoolean()) {
                short size = dataIn.ReadInt16();
                DataStructure[] value = new DataStructure[size];
                for( int i=0; i < size; i++ ) {
                    value[i] = (DataStructure) LooseUnmarshalNestedObject(wireFormat,dataIn);
                }
                info.Data = value;
            }
            else {
                info.Data = null;
            }
        }

        // 
        // Write a object instance to data output stream
        //
        public override void LooseMarshal(OpenWireFormat wireFormat, Object o, BinaryWriter dataOut)
        {

            DataArrayResponse info = (DataArrayResponse)o;

            base.LooseMarshal(wireFormat, o, dataOut);
            LooseMarshalObjectArray(wireFormat, info.Data, dataOut);
        }
    }
}
