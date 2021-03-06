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

namespace Apache.NMS.MSMQ
{
	/// <summary>
	/// A Factory that can estbalish NMS connections to MSMQ
	/// </summary>
	public class ConnectionFactory : IConnectionFactory
	{
		public const string DEFAULT_BROKER_URL = "msmq://localhost";
		public const string ENV_BROKER_URL = "MSMQ_BROKER_URL";

		public static string GetDefaultBrokerUrl()
		{
			string answer = Environment.GetEnvironmentVariable(ENV_BROKER_URL);
			if(answer == null)
			{
				answer = DEFAULT_BROKER_URL;
			}
			return answer;
		}

		public ConnectionFactory()
			: this(GetDefaultBrokerUrl())
		{
		}

		public ConnectionFactory(string brokerUri)
			: this(brokerUri, null)
		{
		}

		public ConnectionFactory(string brokerUri, string clientID)
			: this(new Uri(brokerUri), clientID)
		{
		}

		public ConnectionFactory(Uri brokerUri)
			: this(brokerUri, null)
		{
		}

		public ConnectionFactory(Uri brokerUri, string clientID)
		{

		}

		/// <summary>
		/// Creates a new connection to MSMQ.
		/// </summary>
		public IConnection CreateConnection()
		{
			return new Connection();
		}

		/// <summary>
		/// Creates a new connection to MSMQ.
		/// </summary>
		public IConnection CreateConnection(string userName, string password)
		{
			return new Connection();
		}

		/// <summary>
		/// Creates a new connection to MSMQ.
		/// </summary>
		public IConnection CreateConnection(string userName, string password, bool useLogging)
		{
			return new Connection();
		}
	}
}
