﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using NLog;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleJsonRpc
{
    class BroadcastResponder
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public static async Task StartBroadcastResponder(int rpcServerPort, int broadcasterResponderPort = 8001, CancellationToken? cancellationToken = null)
        {
            logger.Info("Started broadcast responder on port {0}", broadcasterResponderPort);
            CancellationToken token = cancellationToken ?? CancellationToken.None;

            using (var client = new UdpClient(broadcasterResponderPort))
            {
                // setup client for broadcast
                client.EnableBroadcast = true;

                // fire task on cancelation so we can use await whenany
                var tcs = new TaskCompletionSource<UdpReceiveResult>();
                token.Register(() => tcs.TrySetCanceled());
                var cancellationTask = tcs.Task;

                while (true)
                {
                    var completedTask = await Task.WhenAny(client.ReceiveAsync(), cancellationTask);
                    if (completedTask.IsCanceled){
                        logger.Info("Stopping broadcast responder.");
                        break;
                    }

                    // if we're here, we got a UDP message.
                    var udpResult = completedTask.Result;

                    if (Encoding.ASCII.GetString(udpResult.Buffer) == "SimpleJsonRpc Ping")
                    {
                        var clientEndpoint = udpResult.RemoteEndPoint;
                        byte[] resp = BitConverter.GetBytes(rpcServerPort);
                        client.Send(resp, resp.Length, clientEndpoint);

                        logger.Info($"Responded to broadcast packet from: {clientEndpoint.Address}");
                    }
                }
            }
        }
    }
}
