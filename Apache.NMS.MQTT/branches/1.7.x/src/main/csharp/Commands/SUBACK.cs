//
// Licensed to the Apache Software Foundation (ASF) under one or more
// contributor license agreements.  See the NOTICE file distributed with
// this work for additional information regarding copyright ownership.
// The ASF licenses this file to You under the Apache License, Version 2.0
// (the "License"); you may not use this file except in compliance with
// the License.  You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
using System;
using System.IO;
using Apache.NMS.MQTT.Transport;
using Apache.NMS.MQTT.Protocol;

namespace Apache.NMS.MQTT.Commands
{
	/// <summary>
	/// The payload contains a list of granted QoS levels. These are the QoS levels at
	/// which the administrators for the server have permitted the client to subscribe to a
    /// particular Topic Name. Granted QoS levels are listed in the same order as the topic
    /// names in the corresponding SUBSCRIBE message.
	/// </summary>
	public class SUBACK : Response
	{
		public const byte TYPE = 9;
		public const byte DEFAULT_HEADER = 0x90;

		public SUBACK() : base(new Header(DEFAULT_HEADER))
		{
		}

		public SUBACK(Header header) : base(header)
		{
		}

		public override int CommandType
		{
			get { return TYPE; }
		}

		public override bool IsSUBACK
		{
			get { return true; }
		}

        public override void Encode(BinaryWriter writer)
        {
            writer.Write(CommandId);
        }

        public override void Decode(BinaryReader reader)
        {
            CorrelationId = reader.ReadInt16();
        }
	}
}

