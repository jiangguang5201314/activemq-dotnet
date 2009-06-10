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
//
//  NOTE!: This file is autogenerated - do not modify!
//         if you need to make a change, please see the Groovy scripts in the
//         activemq-core module
//


using Apache.NMS.ActiveMQ.State;

namespace Apache.NMS.ActiveMQ.Commands
{
	/// <summary>
	///  The ActiveMQ ConnectionInfo Command
	/// </summary>
	public class ConnectionInfo : BaseCommand
	{
		public const byte ID_ConnectionInfo = 3;

		ConnectionId connectionId;
		string clientId;
		string password;
		string userName;
		BrokerId[] brokerPath;
		bool brokerMasterConnector;
		bool manageable;
		bool clientMaster = true;

		public override string ToString()
		{
			return GetType().Name + "["
				+ " ConnectionId=" + ConnectionId
				+ " ClientId=" + ClientId
				+ " Password=" + Password
				+ " UserName=" + UserName
				+ " BrokerPath=" + BrokerPath
				+ " BrokerMasterConnector=" + BrokerMasterConnector
				+ " Manageable=" + Manageable
				+ " ClientMaster=" + ClientMaster
				+ " ]";

		}

		public override byte GetDataStructureType()
		{
			return ID_ConnectionInfo;
		}


		// Properties

		public ConnectionId ConnectionId
		{
			get { return connectionId; }
			set { this.connectionId = value; }
		}

		public string ClientId
		{
			get { return clientId; }
			set { this.clientId = value; }
		}

		public string Password
		{
			get { return password; }
			set { this.password = value; }
		}

		public string UserName
		{
			get { return userName; }
			set { this.userName = value; }
		}

		public BrokerId[] BrokerPath
		{
			get { return brokerPath; }
			set { this.brokerPath = value; }
		}

		public bool BrokerMasterConnector
		{
			get { return brokerMasterConnector; }
			set { this.brokerMasterConnector = value; }
		}

		public bool Manageable
		{
			get { return manageable; }
			set { this.manageable = value; }
		}

		public bool ClientMaster
		{
			get { return clientMaster; }
			set { this.clientMaster = value; }
		}

		public override Response visit(ICommandVisitor visitor)
		{
			return visitor.processAddConnection(this);
		}
	}
}