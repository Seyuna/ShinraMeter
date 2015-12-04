﻿using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using Tera.Data;
using Tera.Game;
using Tera.Game.Messages;
using Tera.Sniffing;

namespace Tera.DamageMeter
{
    public class NetworkController
    {
        public delegate void ConnectedHandler(string serverName);

        public delegate void DataUpdatedHandler(IEnumerable<PlayerInfo> data, ObservableCollection<Entity> entities);

        private static NetworkController _instance;
        private static TeraData _teraData;
        private Dispatcher _dispatcher;
        private EntityTracker _entityTracker;
        private MessageFactory _messageFactory;
        private PlayerTracker _playerTracker;

        private NetworkController()
        {
            TeraSniffer.Instance.MessageReceived += HandleMessageReceived;
            TeraSniffer.Instance.NewConnection += HandleNewConnection;
        }

        public Dispatcher Dispatcher
        {
            get { return _dispatcher; }
            set
            {
                _dispatcher = value;
                DamageTracker.Instance.Dispatcher = _dispatcher;
            }
        }

        public Entity Encounter { get; set; }

        public static NetworkController Instance => _instance ?? (_instance = new NetworkController());

        public event DataUpdatedHandler DataUpdated;
        public event ConnectedHandler Connected;


        private void HandleNewConnection(Server server)
        {
            _teraData = BasicTeraData.Instance.DataForRegion(server.Region);
            _entityTracker = new EntityTracker();
            _playerTracker = new PlayerTracker(_entityTracker);
            _messageFactory = new MessageFactory(_teraData.OpCodeNamer);
            var handler = Connected;
            handler?.Invoke(server.Name);
        }

        public void Reset()
        {
            DamageTracker.Instance.Reset();
            DamageTracker.Instance.TotalDamageEntity = new ConcurrentDictionary<Entity, long>();
            var handler = DataUpdated;
            handler?.Invoke(DamageTracker.Instance, DamageTracker.Instance.Entities);
        }

        private void HandleMessageReceived(Message obj)
        {
            var message = _messageFactory.Create(obj);
            _entityTracker.Update(message);
            var skillResultMessage = message as EachSkillResultServerMessage;
            if (skillResultMessage == null) return;
            var skillResult = new SkillResult(skillResultMessage, _entityTracker, _playerTracker);
            DamageTracker.Instance.Update(skillResult);

            var handler = DataUpdated;
            handler?.Invoke(DamageTracker.Instance, DamageTracker.Instance.Entities);
        }
    }
}