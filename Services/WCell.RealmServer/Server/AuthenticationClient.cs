/*************************************************************************
 *
 *   file		: AuthenticationClient.cs
 *   copyright		: (C) The WCell Team
 *   email		: info@wcell.org
 *   last changed	: $LastChangedDate: 2009-02-07 02:16:59 +0800 (Sat, 07 Feb 2009) $
 *   last author	: $LastChangedBy: dominikseifert $
 *   revision		: $Rev: 737 $
 *
 *   This program is free software; you can redistribute it and/or modify
 *   it under the terms of the GNU General Public License as published by
 *   the Free Software Foundation; either version 2 of the License, or
 *   (at your option) any later version.
 *
 *************************************************************************/

using System;
using System.ServiceModel;
using System.Threading;
using NLog;
using WCell.Core.Timers;
using WCell.Intercommunication.Client;
using WCell.RealmServer.Lang;
using WCell.RealmServer.Res;
using WCell.Util;
using WCell.Util.NLog;
using WCell.Util.Variables;

namespace WCell.RealmServer.Server
{
	/// <summary>
	/// Provides a client wrapper around the authentication service used for 
	/// authentication-to-realm server communication.
	/// </summary>
	public partial class AuthenticationClient
	{
		protected static Logger log = LogManager.GetCurrentClassLogger();

		[Variable("IPCUpdateInterval")]
		public static int UpdateInterval = 5;

		private AuthenticationClientAdapter m_ClientProxy;
		private string m_netAddr;
		private bool m_IsRunning;
		readonly object lck = new object();
		private readonly NetTcpBinding binding;
		private DateTime lastUpdate;

		private bool m_warned;
		private string m_warnInfo;

		/// <summary>
		/// Initializes this Authentication Client
		/// </summary>
		public AuthenticationClient()
		{
			m_IsRunning = true;
			binding = new NetTcpBinding {Security = {Mode = SecurityMode.None}};
		}

		/// <summary>
		/// If set to false, will disonnect (if connected) and stop trying to re-connect.
		/// </summary>
		public bool IsRunning
		{
			get { return m_IsRunning; }
			set
			{
				m_IsRunning = value;
				ForceUpdate();
			}
		}

		/// <summary>
		/// Whether or not the service channel is open.
		/// </summary>
		public bool IsConnected
		{
			get { return (m_ClientProxy != null && m_ClientProxy.State == CommunicationState.Opened && RealmServer.Instance.IsRunning); }
		}

		/// <summary>
		/// The adapter to the authentication service channel.
		/// </summary>
		public AuthenticationClientAdapter Channel
		{
			get { return m_ClientProxy; }
		}

        /// <summary>
        /// 
        /// </summary>
        public string ChannelId
        {
            get; 
            internal set;
        }

		/// <summary>
		/// Notifies the conection maintenance to be re-scheduled immediately. 
		/// Does not wait for the reconnect attempt to start or finish.
		/// </summary>
		public void ForceUpdate()
		{
			// little trick to force an update
			RearmDisconnectWarning();
			lastUpdate = DateTime.Now - TimeSpan.FromSeconds(UpdateInterval);
		}

		public void StartConnect(string netAddr)
		{
			RearmDisconnectWarning();
			m_netAddr = netAddr;
			m_IsRunning = true;
			if (lastUpdate == default(DateTime))
			{
				RealmServer.Instance.RegisterUpdatable(new SimpleUpdatable(MaintainConnectionCallback));
				lastUpdate = DateTime.Now;
			}
		}

		/// <summary>
		/// Must be executed in RealmServer context
		/// </summary>
		protected bool Connect()
		{
			if (!m_warned)
			{
				AddDisconnectWarningToTitle();
				log.Info(Resources.ConnectingToAuthServer);
			}

			RealmServer.Instance.EnsureContext();

			Disconnect(true);
			
			m_ClientProxy = new AuthenticationClientAdapter(binding, new EndpointAddress(m_netAddr));
			m_ClientProxy.Error += OnError;

			bool conn;

			try
			{
                m_ClientProxy.Open();

				//if (!RealmServer.Instance.IsRegisteredAtAuthServer)
				RealmServer.Instance.RegisterRealm();
				conn = IsConnected;
				lastUpdate = DateTime.Now;
			}
			catch (Exception e)
			{
				m_ClientProxy = null;

				if (e is EndpointNotFoundException)
				{
					if (!m_warned)
					{
						log.Error(Resources.IPCProxyFailed, UpdateInterval);
						m_warned = true;
					}
				}
				else
				{
					LogUtil.ErrorException(e, Resources.IPCProxyFailedException, UpdateInterval);
				}
				conn = false;
			}

			if (conn)
			{
				RearmDisconnectWarning();
				var evt = Connected;
				if (evt != null)
				{
					evt(this, null);
				}
			}
			else
			{
				ScheduleReconnect();
			}
			return conn;
		}

		protected void OnError(Exception ex)
		{
			if (ex is CommunicationException)
			{
				// Connection got interrupted
				log.Warn("Lost connection to AuthServer. Scheduling reconnection attempt...");
			}
			else
			{
				LogUtil.ErrorException(ex, Resources.CommunicationException);
			}
			ScheduleReconnect();
		}

		protected void ScheduleReconnect()
		{
			Disconnect(false);
		}

		private void MaintainConnectionCallback()
		{
			if ((DateTime.Now - lastUpdate).TotalSeconds < UpdateInterval)
			{
				return;
			}

			if (!m_IsRunning)
			{
				if (IsConnected)
				{
					Disconnect(true);
				}
			}
			else if (!IsConnected)
			{
				lock (lck)
				{
					if (!RealmServer.Instance.IsRunning)
					{
						RearmDisconnectWarning();
						return;
					}

					if (Connect())
					{
						log.Info(Resources.IPCProxyReconnected);
					}
				}
			}
			else
			{
				lock (lck)
				{
					if (IsConnected) // check again if we are connected after obtaining the lock
					{
						RealmServer.Instance.UpdateRealm();
						lastUpdate = DateTime.Now;
					}
				}
			}
		}

		protected void Disconnect(bool notify)
		{
			RealmServer.Instance.EnsureContext();

			if (m_ClientProxy != null &&
				m_ClientProxy.State != CommunicationState.Closed &&
			    m_ClientProxy.State != CommunicationState.Closing)
			{
				AddDisconnectWarningToTitle();
				try
				{
					if (notify && m_ClientProxy.State == CommunicationState.Opened)
					{
						RealmServer.Instance.UnregisterRealm();
					}

					lock (lck)
					{
						m_ClientProxy.Close();
						m_ClientProxy = null;
					}
				}
				// ReSharper disable EmptyGeneralCatchClause
				catch
				{
				}
				// ReSharper restore EmptyGeneralCatchClause

				var evt = Disconnected;
				if (evt != null)
				{
					evt(this, null);
				}
			}
		}

		void AddDisconnectWarningToTitle()
		{
			m_warnInfo = " - ######### " + RealmLocalizer.Instance.Translate(RealmLangKey.NotConnectedToAuthServer).ToUpper() + " #########";
			Console.Title += m_warnInfo;
		}

		void RearmDisconnectWarning()
		{
			m_warned = false;
			if (m_warnInfo != null)
			{
				Console.Title = Console.Title.Replace(m_warnInfo, "");
				m_warnInfo = null;	
			}
		}
	}
}