/*************************************************************************
 *
 *   file		: GameObject.cs
 *   copyright		: (C) The WCell Team
 *   email		: info@wcell.org
 *   last changed	: $LastChangedDate: 2010-02-17 05:08:19 +0100 (on, 17 feb 2010) $
 *   last author	: $LastChangedBy: dominikseifert $
 *   revision		: $Rev: 1256 $
 *
 *   This program is free software; you can redistribute it and/or modify
 *   it under the terms of the GNU General Public License as published by
 *   the Free Software Foundation; either version 2 of the License, or
 *   (at your option) any later version.
 *
 *************************************************************************/

using System;
using System.Linq;
using System.Threading;
using NLog;
using WCell.Constants.GameObjects;
using WCell.Constants.Looting;
using WCell.Constants.Updates;
using WCell.Core;
using WCell.Core.Network;
using WCell.Core.Timers;
using WCell.RealmServer.Factions;
using WCell.RealmServer.GameObjects;
using WCell.RealmServer.GameObjects.GOEntries;
using WCell.RealmServer.GameObjects.Spawns;
using WCell.RealmServer.Global;
using WCell.RealmServer.Looting;
using WCell.RealmServer.Misc;
using WCell.RealmServer.Network;
using WCell.RealmServer.Quests;
using WCell.RealmServer.UpdateFields;
using WCell.Util;
using WCell.Util.Graphics;

namespace WCell.RealmServer.Entities
{
	/// <summary>
	/// TODO: Respawning
	/// </summary>
	public partial class GameObject : WorldObject, IOwned, ILockable, IQuestHolder
	{
		private static readonly Logger log = LogManager.GetCurrentClassLogger();

		public static readonly UpdateFieldCollection UpdateFieldInfos = UpdateFieldMgr.Get(ObjectTypeId.GameObject);

		public override UpdateFieldHandler.DynamicUpdateFieldHandler[] DynamicUpdateFieldHandlers
		{
			get { return UpdateFieldHandler.DynamicGOHandlers; }
		}

		internal static int _lastGOUID;

		protected GOEntry m_entry;
		protected Faction m_faction;
		protected GOSpawnPoint m_spawnPoint;

		protected GameObjectHandler m_handler;
		protected bool m_respawns;
		protected TimerEntry m_decayTimer;
		protected GameObject m_linkedTrap;
		protected internal bool m_IsTrap;

		/// <summary>
		/// Use the <c>Create()</c> method to create new GameObjects
		/// </summary>
		public GameObject()
		{
		}

		protected override UpdateFieldCollection _UpdateFieldInfos
		{
			get { return UpdateFieldInfos; }
		}

		public GameObjectHandler Handler
		{
			get { return m_handler; }
			set
			{
				m_handler = value;
				m_handler.Initialize(this);
			}
		}

		public override string Name
		{
			get { return m_entry != null ? m_entry.DefaultName : ""; }
			set
			{
				throw new NotImplementedException("Dynamic renaming of GOs is not implementable.");
			}
		}

		public GOEntry Entry
		{
			get { return m_entry; }
		}

		public override ObjectTemplate Template
		{
			get { return Entry; }
		}

		/// <summary>
		/// The Template of this GO (if any was used)
		/// </summary>
		public GOSpawnPoint SpawnPoint
		{
			get { return m_spawnPoint; }
		}

		/// <summary>
		/// Traps get removed when their AreaAura gets removed
		/// </summary>
		public override bool IsTrap
		{
			get { return m_IsTrap; }
		}

		public override ObjectTypeId ObjectTypeId
		{
			get { return ObjectTypeId.GameObject; }
		}

		public override UpdateFlags UpdateFlags
		{
			get { return UpdateFlags.StationaryObject | UpdateFlags.Flag_0x10 | UpdateFlags.HasRotation | UpdateFlags.StationaryObjectOnTransport; }
		}

		#region Locks and Loot

		public LockEntry Lock
		{
			get { return m_entry.Lock; }
		}

		public override void OnFinishedLooting()
		{
			if (m_entry.IsConsumable)
			{
				Delete();
			}
		}

		public override uint GetLootId(LootEntryType type)
		{
			if (m_entry is IGOLootableEntry)
			{
				return ((IGOLootableEntry)m_entry).LootId;
			}
			return 0;
		}

		public override bool UseGroupLoot
		{
			get { return m_entry.UseGroupLoot; }
		}
		#endregion

		/// <summary>
		/// Creates the given kind of GameObject with the default Template
		/// </summary>
		public static GameObject Create(GOEntryId id, IWorldLocation location, GOSpawnEntry spawnEntry = null, GOSpawnPoint spawnPoint = null)
		{
			var entry = GOMgr.GetEntry(id);
			if (entry == null)
			{
				return null;
			}
			return Create(entry, location, spawnEntry, spawnPoint);
		}

		/// <summary>
		/// Creates a new GameObject with the given parameters
		/// </summary>
		public static GameObject Create(GOEntryId id, Map map, GOSpawnEntry spawnEntry = null, GOSpawnPoint spawnPoint = null)
		{
			var entry = GOMgr.GetEntry(id);
			if (entry != null)
			{
				return Create(entry, map, spawnEntry, spawnPoint);
			}
			return null;
		}

		public static GameObject Create(GOEntry entry, Map map, GOSpawnEntry spawnEntry = null, GOSpawnPoint spawnPoint = null)
		{
			return Create(entry, new WorldLocation(map, Vector3.Zero), spawnEntry, spawnPoint);
		}

		/// <summary>
		/// Creates a new GameObject with the given parameters
		/// </summary>
		public static GameObject Create(GOEntry entry, IWorldLocation where, GOSpawnEntry spawnEntry = null, GOSpawnPoint spawnPoint = null)
		{
			var go = entry.GOCreator();
			var handlerCreator = entry.HandlerCreator;
			go.Init(entry, spawnEntry, spawnPoint);
			if (handlerCreator != null)
			{
				go.Handler = handlerCreator();
			}
			else
			{
				log.Warn("GOEntry {0} did not have a HandlerCreator set - Type: {1}", entry, entry.Type);
				go.Delete();
				return null;
			}
			go.Phase = where.Phase;
			var pos = where.Position;
			where.Map.AddObject(go, ref pos);
			return go;
		}

		/// <summary>
		/// Initialize the GO
		/// </summary>
		/// <param name="entry"></param>
		/// <param name="templ"></param>
		internal virtual void Init(GOEntry entry, GOSpawnEntry spawnEntry, GOSpawnPoint spawnPoint)
		{
			EntityId = EntityId.GetGameObjectId((uint)Interlocked.Increment(ref _lastGOUID), entry.GOId);
			Type |= ObjectTypes.GameObject;
			//DynamicFlagsLow = GameObjectDynamicFlagsLow.Activated;
			m_entry = entry;
			m_spawnPoint = spawnPoint;

			DisplayId = entry.DisplayId;
			EntryId = entry.Id;
			GOType = entry.Type;
			Flags = m_entry.Flags;
			m_faction = m_entry.Faction ?? Faction.NullFaction;
			ScaleX = m_entry.Scale;

			spawnEntry = spawnEntry ?? entry.FirstSpawnEntry;
			if (spawnEntry != null)
			{
				Phase = spawnEntry.Phase;
				State = spawnEntry.State;
				if (spawnEntry.Scale != 1)
				{
					ScaleX = spawnEntry.Scale;
				}
				Orientation = spawnEntry.Orientation;
				AnimationProgress = spawnEntry.AnimProgress;
				SetRotationFields(spawnEntry.Rotations);
			}

			m_entry.InitGO(this);
		}

		private static readonly double RotatationConst = Math.Atan(Math.Pow(2.0f, -20.0f));

		protected void SetRotationFields(float[] rotations)
		{
			if (rotations.Length != 4)
				return;

			SetFloat(GameObjectFields.PARENTROTATION + 0, rotations[0]);
			SetFloat(GameObjectFields.PARENTROTATION + 1, rotations[1]);

			double rotSin = Math.Sin(Orientation / 2.0f),
				   rotCos = Math.Cos(Orientation / 2.0f);

			Rotation = (long)(rotSin / RotatationConst * (rotCos >= 0 ? 1.0f : -1.0f)) & 0x1FFFFF;

			if (rotations[2] == 0 && rotations[3] == 0)
			{
				SetFloat(GameObjectFields.PARENTROTATION + 2, (float)rotSin);
				SetFloat(GameObjectFields.PARENTROTATION + 3, (float)rotCos);
			}
			else
			{
				SetFloat(GameObjectFields.PARENTROTATION + 2, rotations[2]);
				SetFloat(GameObjectFields.PARENTROTATION + 3, rotations[3]);
			}
		}

		protected internal override void OnEnterMap()
		{
			// add Trap
			if (m_entry.LinkedTrap != null)
			{
				m_linkedTrap = m_entry.LinkedTrap.Spawn(this, m_master);
				//if (m_entry.LinkedTrap.DisplayId != 0)
				//{
				//    m_linkedTrap = m_entry.LinkedTrap.Spawn(m_map, m_position, m_Owner);
				//}
				//else
				//{
				//    ActivateTrap(m_entry.LinkedTrap);
				//}
			}

			// add to set of spawned objects of SpawnPoint
			if (m_spawnPoint != null)
			{
				m_spawnPoint.SignalSpawnlingActivated(this);
			}

			// trigger events
			m_entry.NotifyActivated(this);
		}

		protected internal override void OnLeavingMap()
		{
			if (m_master is Character)
			{
				if (m_master.IsInWorld)
				{
					((Character)m_master).OnOwnedGODestroyed(this);
				}
				//Delete();
			}
			m_handler.OnRemove();
			SendDespawn();
			base.OnLeavingMap();
		}

		protected override UpdateType GetCreationUpdateType(UpdateFieldFlags relation)
		{
			if (m_entry is GODuelFlagEntry)
			{
				return UpdateType.CreateSelf;
			}
			return UpdateType.Create;
		}

		public bool IsCloseEnough(Unit unit, float radius)
		{
			return (unit.IsInRadius(this, radius)) || (unit is Character && ((Character)unit).Role.IsStaff);
		}

		public override string ToString()
		{
			return m_entry.DefaultName + " (SpawnPoint: " + m_spawnPoint + ")";
		}

		public bool IsCloseEnough(Character chr)
		{
			return IsInRadius(chr.Position, 10.0f);
		}

		public bool CanUseInstantly(Character chr)
		{
			if (!IsCloseEnough(chr))
			{
				return false;
			}

			if (Entry.Type == GameObjectType.Chest && !Flags.HasFlag(GameObjectFlags.ConditionalInteraction))
			{
				// conditional chests are chests that contain quest items
				// other chests must be opened through a spell
				return false;
			}
			return CanInteractWith(chr);
		}

		public bool CanInteractWith(Character chr)
		{
			if (IsEnabled)
			{
				if (Flags.HasFlag(GameObjectFlags.ConditionalInteraction))
				{
					// must have the quest
					return Entry.RequiredQuests.Any(q => chr.QuestLog.HasActiveQuest(q));
				}
				// can always interact
				return true;
			}
			return false;
		}

		#region Handling

		/// <summary>
		/// Makes the given Unit use this GameObject.
		/// Skill-locked GameObjects cannot be used directly but must be interacted on with spells.
		/// </summary>
		public bool Use(Character chr)
		{
			if ((Lock == null || Lock.IsUnlocked || Lock.Keys.Length > 0) &&
				Handler.TryUse(chr))
			{
				chr.QuestLog.OnUse(this);
				return true;
			}
			return false;
		}

		/// <summary>
		/// Lets the given user try to loot this object.
		/// Called on Chests automatically when using Chest-GOs.
		/// </summary>
		public bool TryLoot(Character chr)
		{
			return ((ILockable)this).TryLoot(chr);
		}

		#endregion

		#region Decay
		void DecayNow(int dt)
		{
			Delete();
		}

		protected internal override void DeleteNow()
		{
			if (m_spawnPoint != null)
			{
				m_spawnPoint.SignalSpawnlingDied(this);
			}
			if (m_linkedTrap != null)
			{
				m_linkedTrap.DeleteNow();
			}
			base.DeleteNow();
		}

		void StopDecayTimer()
		{
			if (m_decayTimer != null)
			{
				m_decayTimer.Stop();
				m_decayTimer = null;
			}
		}

		/// <summary>
		/// Can be set to initialize Decay after the given delay in seconds.
		/// Will stop the timer if set to a value less than 0
		/// </summary>
		public int RemainingDecayDelayMillis
		{
			get
			{
				return m_decayTimer.RemainingInitialDelayMillis;
			}
			set
			{
				if (value < 0)
				{
					StopDecayTimer();
				}
				else
				{
					m_decayTimer = new TimerEntry(DecayNow);
					m_decayTimer.Start(value, 0);
				}
			}
		}

		public override void Update(int dt)
		{
			base.Update(dt);
			if (m_decayTimer != null)
			{
				m_decayTimer.Update(dt);
			}
		}

		#endregion

		#region Update
		protected override void WriteMovementUpdate(PrimitiveWriter packet, UpdateFieldFlags relation)
		{
			// StationaryObjectOnTransport
			if (UpdateFlags.HasAnyFlag(UpdateFlags.StationaryObjectOnTransport))
			{
				EntityId.Zero.WritePacked(packet);
				packet.Write(Position);
				packet.Write(Position); // transport position, but server seemed to send normal position except orientation
				packet.Write(Orientation);
				packet.Write(0.0f);
			}
			else if (UpdateFlags.HasAnyFlag(UpdateFlags.StationaryObject))
			{
				#region UpdateFlag.Flag_0x40 (StationaryObject)

				packet.Write(Position);
				packet.WriteFloat(Orientation);

				#endregion
			}
		}

		protected override void WriteTypeSpecificMovementUpdate(PrimitiveWriter writer, UpdateFieldFlags relation, UpdateFlags updateFlags)
		{
			// Will only be GameObjects
			if (updateFlags.HasAnyFlag(UpdateFlags.Transport))
			{
				writer.Write(Utility.GetSystemTime());
			}
			if (updateFlags.HasAnyFlag(UpdateFlags.HasRotation))
			{
				writer.Write(Rotation);
			}
		}
		#endregion
	}
}