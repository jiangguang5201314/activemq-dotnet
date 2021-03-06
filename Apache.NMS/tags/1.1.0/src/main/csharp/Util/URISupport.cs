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
using System.Collections.Specialized;
using System.Globalization;
using System.Reflection;
using System.Text;
using Apache.NMS;
#if !NETCF
using System.Web;
#endif

namespace Apache.NMS.Util
{
	/// <summary>
	/// Class to provide support for Uri query parameters which uses .Net reflection
	/// to identify and set properties.
	/// </summary>
	public class URISupport
	{
		/// <summary>
		/// Parse a Uri query string of the form ?x=y&amp;z=0
		/// into a map of name/value pairs.
		/// </summary>
		/// <param name="query">The query string to parse. This string should not contain
		/// Uri escape characters.</param>
		public static StringDictionary ParseQuery(string query)
		{
			StringDictionary map = new StringDictionary();

			// strip the initial "?"
			if(query.StartsWith("?"))
			{
				query = query.Substring(1);
			}

			// split the query into parameters
			string[] parameters = query.Split('&');
			foreach(string pair in parameters)
			{
				if(pair.Length > 0)
				{
					string[] nameValue = pair.Split('=');

					if(nameValue.Length != 2)
					{
						throw new NMSException(string.Format("Invalid Uri parameter: {0}", query));
					}

					map[nameValue[0]] = nameValue[1];
				}
			}

			return map;
		}

		public static StringDictionary ParseParameters(Uri uri)
		{
			return (uri.Query == null
					? emptyMap
					: ParseQuery(stripPrefix(uri.Query, "?")));
		}

		/// <summary>
		/// Sets the public properties of a target object using a string map.
		/// This method uses .Net reflection to identify public properties of
		/// the target object matching the keys from the passed map.
		/// </summary>
		/// <param name="target">The object whose properties will be set.</param>
		/// <param name="map">Map of key/value pairs.</param>
		/// <param name="prefix">Key value prefix.  This is prepended to the property name
		/// before searching for a matching key value.</param>
		public static void SetProperties(object target, StringDictionary map, string prefix)
		{
			Type type = target.GetType();

			foreach(string key in map.Keys)
			{
				if(key.ToLower().StartsWith(prefix.ToLower()))
				{
					string bareKey = key.Substring(prefix.Length);
					PropertyInfo prop = type.GetProperty(bareKey,
															BindingFlags.FlattenHierarchy
															| BindingFlags.Public
															| BindingFlags.Instance
															| BindingFlags.IgnoreCase);

					if(null == prop)
					{
						throw new NMSException(string.Format("no such property: {0} on class: {1}", bareKey, target.GetType().Name));
					}

					prop.SetValue(target, Convert.ChangeType(map[key], prop.PropertyType, CultureInfo.InvariantCulture), null);
				}
			}
		}

		public static String UrlDecode(String s)
		{
#if !NETCF
			return HttpUtility.HtmlDecode(s);
#else
			return Uri.UnescapeDataString(s);
#endif
		}

		public static String UrlEncode(String s)
		{
#if !NETCF
			return HttpUtility.HtmlEncode(s);
#else
			return Uri.EscapeUriString(s);
#endif
		}

		public static String createQueryString(StringDictionary options)
		{
			if(options.Count > 0)
			{
				StringBuilder rc = new StringBuilder();
				bool first = true;

				foreach(String key in options.Keys)
				{
					string value = options[key];

					if(first)
					{
						first = false;
					}
					else
					{
						rc.Append("&");
					}

					rc.Append(UrlEncode(key));
					rc.Append("=");
					rc.Append(UrlEncode(value));
				}

				return rc.ToString();
			}
			else
			{
				return "";
			}
		}

		public class CompositeData
		{
			private String host;
			private String scheme;
			private String path;
			private Uri[] components;
			private StringDictionary parameters;
			private String fragment;

			public Uri[] Components
			{
				get { return components; }
				set { components = value; }
			}

			public String Fragment
			{
				get { return fragment; }
				set { fragment = value; }
			}

			public StringDictionary Parameters
			{
				get { return parameters; }
				set { parameters = value; }
			}

			public String Scheme
			{
				get { return scheme; }
				set { scheme = value; }
			}

			public String Path
			{
				get { return path; }
				set { path = value; }
			}

			public String Host
			{
				get { return host; }
				set { host = value; }
			}

			public Uri toUri()
			{
				StringBuilder sb = new StringBuilder();
				if(scheme != null)
				{
					sb.Append(scheme);
					sb.Append(':');
				}

				if(host != null && host.Length != 0)
				{
					sb.Append(host);
				}
				else
				{
					sb.Append('(');
					for(int i = 0; i < components.Length; i++)
					{
						if(i != 0)
						{
							sb.Append(',');
						}
						sb.Append(components[i].ToString());
					}
					sb.Append(')');
				}

				if(path != null)
				{
					sb.Append('/');
					sb.Append(path);
				}

				if(parameters.Count != 0)
				{
					sb.Append("?");
					sb.Append(createQueryString(parameters));
				}

				if(fragment != null)
				{
					sb.Append("#");
					sb.Append(fragment);
				}

				return new Uri(sb.ToString());
			}
		}

		public static String stripPrefix(String value, String prefix)
		{
			if(value.StartsWith(prefix))
			{
				return value.Substring(prefix.Length);
			}

			return value;
		}

		public static CompositeData parseComposite(Uri uri)
		{
			CompositeData rc = new CompositeData();
			rc.Scheme = uri.Scheme;

			// URI is one of these formats:
			//     scheme://host:port/path?query
			//     scheme://host/path(URI_1,URI_2,...,URI_N)?query
			// where URI_x can be any valid URI (including a composite one).
			// Host and port and path are optional.
			// This does mean that a URI containing balanced parenthesis are considered
			// to be composite. This can be a problem if parenthesis are used in another context.
			//
			// This routine constructs CompositeData reflecting either of
			// these forms. Each of the URI_x are stored into the components
			// of the CompositeData.
			//
			// Sample valid URI that should be accepted:
			//
			// tcp://192.168.1.1
			// tcp://192.168.1.1/
			// tcp://192.168.1.1:61616
			// tcp://192.168.1.1:61616/
			// tcp://machine:61616
			// tcp://host:61616/
			// failover:(tcp://192.168.1.1:61616?trace=true,tcp://machine:61616?trace=false)?random=true

			// If this is a composite URI, then strip the scheme
			// and break up the URI into components. If not, then pass the URI directly.
			// We detect "compositeness" by the existence of a "(" in the URI containing
			// balanced parenthesis

			// Start with original URI
#if NET_1_0 || NET_1_1
            String ssp = uri.AbsoluteUri.Trim();
#else
            String ssp = uri.OriginalString.Trim();
#endif

			ssp = stripPrefix(ssp, "failover:");

			// Handle the composite components
			parseComposite(uri, rc, ssp);
			rc.Fragment = uri.Fragment;
			return rc;
		}

		/// <summary>
		/// </summary>
		/// <param name="uri"></param>
		/// <param name="rc"></param>
		/// <param name="ssp"></param>
		private static void parseComposite(Uri uri, CompositeData rc, String ssp)
		{
			String componentString;
			String parms;

			if(!checkParenthesis(ssp))
			{
				throw new NMSException(uri.ToString() + ": Not a matching number of '(' and ')' parenthesis");
			}

			int p;
			int intialParen = ssp.IndexOf("(");

			if(intialParen >= 0)
			{
				rc.Host = ssp.Substring(0, intialParen);
				p = rc.Host.IndexOf("/");
				if(p >= 0)
				{
					rc.Path = rc.Host.Substring(p);
					rc.Host = rc.Host.Substring(0, p);
				}

				p = ssp.LastIndexOf(")");
				int start = intialParen + 1;
				int len = p - start;
				componentString = ssp.Substring(start, len);
				parms = ssp.Substring(p + 1).Trim();
			}
			else
			{
				p = ssp.IndexOf("?");
				if(p >= 0)
				{
					componentString = ssp.Substring(0, p);
					parms = ssp.Substring(p);
				}
				else
				{
					componentString = ssp;
					parms = "";
				}
			}

			String[] components = splitComponents(componentString);
			rc.Components = new Uri[components.Length];
			for(int i = 0; i < components.Length; i++)
			{
				rc.Components[i] = new Uri(components[i].Trim());
			}

			p = parms.IndexOf("?");
			if(p >= 0)
			{
				if(p > 0)
				{
					rc.Path = stripPrefix(parms.Substring(0, p), "/");
				}

				rc.Parameters = ParseQuery(parms.Substring(p + 1));
			}
			else
			{
				if(parms.Length > 0)
				{
					rc.Path = stripPrefix(parms, "/");
				}

				rc.Parameters = emptyMap;
			}
		}

		private static StringDictionary emptyMap
		{
			get { return new StringDictionary(); }
		}

		/// <summary>
		/// </summary>
		/// <param name="componentString"></param>
		private static String[] splitComponents(String componentString)
		{
			ArrayList l = new ArrayList();

			int last = 0;
			int depth = 0;
			char[] chars = componentString.ToCharArray();
			for(int i = 0; i < chars.Length; i++)
			{
				switch(chars[i])
				{
				case '(':
					depth++;
					break;

				case ')':
					depth--;
					break;

				case ',':
					if(depth == 0)
					{
						String s = componentString.Substring(last, i - last);
						l.Add(s);
						last = i + 1;
					}
					break;

				default:
					break;
				}
			}

			String ending = componentString.Substring(last);
			if(ending.Length != 0)
			{
				l.Add(ending);
			}

			String[] rc = new String[l.Count];
			l.CopyTo(rc);
			return rc;
		}

		public static bool checkParenthesis(String str)
		{
			bool result = true;

			if(str != null)
			{
				int open = 0;
				int closed = 0;

				int i = 0;
				while((i = str.IndexOf('(', i)) >= 0)
				{
					i++;
					open++;
				}

				i = 0;
				while((i = str.IndexOf(')', i)) >= 0)
				{
					i++;
					closed++;
				}

				result = (open == closed);
			}

			return result;
		}
	}
}
