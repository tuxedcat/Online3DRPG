﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TeraTaleNet;

namespace Proxy
{
    partial class Proxy : NetworkProgram, MessageHandler, IDisposable
    {
        NetworkAgent _agent = new NetworkAgent();
        Messenger _messenger;
        Messenger _confirmMessenger;
        List<string> _serverKeys = new List<string>();
        Dictionary<string, Task> _dispatchers = new Dictionary<string, Task>();
        Dictionary<string, string> _worldByUser = new Dictionary<string, string>();
        Task _accepter;
        Task _confirm;
        Task _client;
        int _currentConfirmId = 0;
        object _lock = new object();
        Dictionary<string, List<RPC>> _rpcBufferByWorld = new Dictionary<string, List<RPC>>();
        int _currentNetworkID = 1;

        public Task Client
        {
            get { return _client; }
            set { _client = value; }
        }

        protected override void OnStart()
        {
            _messenger = new Messenger(this);
            _confirmMessenger = new Messenger(this);

            Action<string, Port> connector = (string ip, Port port) =>
            {
                var stream = _agent.Connect(ip, port);
                stream.Write(new ConnectorInfo("Proxy"));
                _messenger.Register(port.ToString(), stream);
                Console.WriteLine(port.ToString() + " connected.");
                _worldByUser.Add(port.ToString(), port.ToString());
                _serverKeys.Add(port.ToString());
                _rpcBufferByWorld.Add(port.ToString(), new List<RPC>());
            };

            connector("127.0.0.1", Port.Database);
            connector("127.0.0.1", Port.Login);
            connector("127.0.0.1", Port.Town);
            connector("127.0.0.1", Port.Forest);
            connector("127.0.0.1", Port.Mine);
            connector("127.0.0.1", Port.Boss);

            foreach (var key in _messenger.Keys)
            {
                var dispatcher = Task.Run(() =>
                {
                    while (stopped == false)
                        _messenger.Dispatch(key);
                });
                _dispatchers.Add(key, dispatcher);
            }

            _accepter = Task.Run(() =>
            {
                _agent.Bind("127.0.0.1", Port.Proxy, 4);
                while (stopped == false)
                {
                    try
                    {
                        if (_agent.HasConnectReq())
                        {
                            string stringID = _currentConfirmId.ToString();
                            int ID = _currentConfirmId;
                            lock (_lock)
                            {
                                _confirmMessenger.Register(stringID, _agent.Listen());
                                _confirmMessenger.Send(stringID, new ConfirmID(ID));
                            }
                            _currentConfirmId++;
                            Console.WriteLine("Client connected.");
                        }
                    }
                    catch (Exception e)
                    {
                        History.Log(e.ToString());
                    }
                }
            });

            _confirm = Task.Run(() =>
            {
                while (stopped == false)
                {
                    try
                    {
                        lock (_lock)
                        {
                            foreach (var confirmID in _confirmMessenger.Keys)
                                _confirmMessenger.Dispatch(confirmID);
                        }
                    }
                    catch (Exception e)
                    {
                        History.Log(e.ToString());
                    }
                }
            });

            _client = Task.Run(() =>
            {
                while (stopped == false)
                {
                    try
                    {
                        lock (_lock)
                        {
                            foreach (var key in _messenger.Keys)
                            {
                                if (_serverKeys.Contains(key) == false)
                                    _messenger.Dispatch(key);
                            }
                        }

                    }
                    catch (Exception e)
                    {
                        History.Log(e.ToString());
                    }
                }
            });

            _messenger.Start();
            _messenger.onDisconnected = key =>
            {
                foreach (var target in _messenger.Keys)
                {
                    if (target != key && _worldByUser[target] == _worldByUser[key])
                        _messenger.Send(target, new PlayerDisconnect(key));
                }
                _rpcBufferByWorld[_worldByUser[key]].RemoveAll(rpc => rpc.sender == key);
                _dispatchers.Remove(key);
                _worldByUser.Remove(key);
            };
            _confirmMessenger.Start();
        }

        protected override void OnEnd()
        {
            _accepter.Wait();
            foreach (var task in _dispatchers.Values)
                task.Wait();
            _confirm.Wait();
            _client.Wait();
            Dispose();
        }

        protected override void OnUpdate()
        {
            if (Console.KeyAvailable)
            {
                if (Console.ReadKey(true).Key == ConsoleKey.Escape)
                    Stop();
            }
        }

        public void Dispose()
        {
            _messenger.Join();
            _confirmMessenger.Join();
            _agent.Dispose();
            GC.SuppressFinalize(this);
        }

        void SignUp(Messenger messenger, string key, SignUp packet)
        {
            _messenger.Send("Database", packet);
        }

        void LoginQuery(Messenger messenger, string key, LoginQuery query)
        {
            _messenger.Send("Login", query);
        }

        void LoginAnswer(Messenger messenger, string key, LoginAnswer answer)
        {
            if (answer.accepted)
            {
                if (IsLoggedIn(answer.name))
                {
                    answer.accepted = false;
                    answer.message = "이미 로그인 중입니다.";
                    _confirmMessenger.Send(answer.confirmID.ToString(), answer);
                }
                else
                {
                    try
                    {
                        lock (_lock)
                        {
                            PacketStream stream = _confirmMessenger.Unregister(answer.confirmID.ToString());
                            AddClient(answer.name, stream);
                        }
                        try
                        {
                            _worldByUser.Add(answer.name, answer.world);
                        }
                        catch (ArgumentException e)
                        {
                            History.Log(e.ToString());
                            _worldByUser[answer.name] = answer.world;
                        }
                        _messenger.Send(answer.name, answer);
                    }
                    catch(Exception e)
                    {
                        History.Log(e.ToString());
                    }
                }
            }
            else
            {
                _confirmMessenger.Send(answer.confirmID.ToString(), answer);
            }
        }

        bool IsLoggedIn(string user)
        {
            var keys = (ICollection<string>)_messenger.Keys;
            return keys.Contains(user);
        }

        void AddClient(string name, PacketStream stream)
        {
            _messenger.Register(name, stream);
        }

        void MessageHandler.RPCHandler(RPC rpc)
        {
            if ((rpc.rpcType & RPCType.Self) != 0)
            {
                _messenger.Send(rpc.sender, rpc);
            }
            //if ((rpc.rpcType & RPCType.Server) == RPCType.Server)
            //{
            //    _messenger.Send(_worldByUser[rpc.sender], rpc);
            //}
            if ((rpc.rpcType & RPCType.Others) != 0)
            {
                foreach (var target in _messenger.Keys)
                {
                    if (target != rpc.sender && _worldByUser[target] == _worldByUser[rpc.sender])
                        _messenger.Send(target, rpc);
                }
            }
            if ((rpc.rpcType & RPCType.Buffered) != 0)
            {
                _rpcBufferByWorld[_worldByUser[rpc.sender]].Add(rpc);
            }
            if ((rpc.rpcType & RPCType.Specific) != 0)
            {
                _messenger.Send(rpc.receiver, rpc);
            }
        }

        void NetworkInstantiate(NetworkInstantiate rpc)
        {
            rpc.networkID = _currentNetworkID++;
        }

        void SwitchWorld(SwitchWorld rpc)
        {
            _worldByUser[rpc.user] = rpc.world;
        }

        void SendBufferedRPC(string user)
        {
            foreach (var i in _rpcBufferByWorld[_worldByUser[user]])
                _messenger.Send(user, i);
        }

        void RemoveBufferedRPC(Messenger messenger, string key, RemoveBufferedRPC packet)
        {
            var buffer = _rpcBufferByWorld[_worldByUser[packet.caller]];
            for (int i = 0; i < buffer.Count; i++)
            {
                if (buffer[i].GetType().Name == packet.typeName)
                {
                    NetworkInstantiate ni = buffer[i] as NetworkInstantiate;
                    if (ni != null && ni.networkID == packet.networkID)
                        buffer.RemoveAt(i);
                    else
                    {
                        Sync sync = buffer[i] as Sync;
                        if (sync != null && sync.signallerID == packet.networkID)
                            buffer.RemoveAt(i);
                    }
                }
            }
        }

        void BufferedRPCRequest(Messenger messenger, string key, BufferedRPCRequest packet)
        {
            SendBufferedRPC(packet.caller);
        }
    }
}