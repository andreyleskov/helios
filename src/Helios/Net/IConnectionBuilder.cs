﻿// Copyright (c) Petabridge <https://petabridge.com/>. All rights reserved.
// Licensed under the Apache 2.0 license. See LICENSE file in the project root for full license information.
// See ThirdPartyNotices.txt for references to third party code used inside Helios.

using System;
using Helios.Topology;

namespace Helios.Net
{
    /// <summary>
    ///     Interface used for building connections
    /// </summary>
    public interface IConnectionBuilder
    {
        TimeSpan Timeout { get; }

        IConnection BuildConnection(INode node);
    }
}