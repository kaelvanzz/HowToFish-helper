using System;
using System.Collections.Generic;
using FishNet;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Serializing;
using FishNet.Transporting;
using UnityEngine;

namespace HTFMod;

/// <summary>Cheat requests from a modded client to the modded host (executed with server authority).</summary>
public enum CheatAction : byte
{
    Hello = 1,
    AddMoney = 2,
    GodSelf = 3,
    BuiltInCheats = 5,
    VitalsLock = 6,
    NextIsland = 7,
    KillBoss = 8,
    KillAllCreatures = 9,
    RespawnCreatures = 10,
    FishRain = 11,
    SpawnItem = 12,
    RouletteAutoWin = 13,
    KeepInventorySelf = 14,
}

public struct CheatRequest : IBroadcast
{
    public byte Action;
    public int Amount;
    public byte Currency;
    public bool Flag;
    public int ItemId;
}

public struct CheatReply : IBroadcast
{
    public byte Action;
    public bool Ok;
    public int Value;
}

/// <summary>
/// Mod-to-mod online bridge: when both sides run HTFMod, client menu actions are sent
/// to the host over FishNet broadcasts and executed there with server authority
/// (money/spawn/islands/boss/one-shot/roulette auto-win/...). With a vanilla host the
/// server-side buttons stay disabled.
/// </summary>
public static class Multiplayer
{
    private static NetworkManager _registeredNm;
    private static readonly HashSet<Player> _vitalsLock = new();

    // Host-side "self-only" registrations (clients register via the bridge; the damage
    // and drop entries consult these sets).
    private static readonly HashSet<NetworkConnection> _godProtected = new();
    private static readonly HashSet<NetworkConnection> _keepInvProtected = new();

    /// <summary>Client-side: true once the host mod answered the handshake.</summary>
    public static bool HostModOnline { get; private set; }

    /// <summary>Host-side: remote players requesting the fullness lock.</summary>
    public static IReadOnlyCollection<Player> VitalsLockPlayers => _vitalsLock;

    public static bool HasGodProtected => _godProtected.Count > 0;

    public static bool IsGodProtected(NetworkConnection c) => c != null && _godProtected.Contains(c);

    public static bool IsKeepInvProtected(NetworkConnection c) => c != null && _keepInvProtected.Contains(c);

    public static void Tick()
    {
        var nm = InstanceFinder.NetworkManager;
        if (nm == null)
        {
            HostModOnline = false;
            return;
        }
        if (_registeredNm != nm)
            Register(nm);

        var cm = InstanceFinder.ClientManager;
        if (cm == null || !cm.Started)
            HostModOnline = false;
    }

    /// <summary>Client: called when the menu opens; handshakes with the host if not yet connected.</summary>
    public static void TryHandshake()
    {
        if (HostModOnline)
            return;
        var cm = InstanceFinder.ClientManager;
        if (cm == null || !cm.Started)
            return;
        cm.Broadcast(new CheatRequest { Action = (byte)CheatAction.Hello }, Channel.Reliable);
    }

    // ────────────────────────── registration ──────────────────────────

    private static void Register(NetworkManager nm)
    {
        try
        {
            RegisterSerializers();

            nm.ServerManager.RegisterBroadcast<CheatRequest>(OnServerRequest, requireAuthentication: true);
            nm.ClientManager.RegisterBroadcast<CheatReply>(OnClientReply);
            _registeredNm = nm;
            Loader.Log("Online bridge registered (broadcast)");
        }
        catch (Exception e)
        {
            Loader.Log("Online bridge registration failed: " + e);
        }
    }

    private static void RegisterSerializers()
    {
        GenericWriter<CheatRequest>.SetWrite((Writer w, CheatRequest m) =>
        {
            w.WriteUInt8Unpacked(m.Action);
            w.WriteInt32(m.Amount);
            w.WriteUInt8Unpacked(m.Currency);
            w.WriteBoolean(m.Flag);
            w.WriteInt32(m.ItemId);
        });
        GenericReader<CheatRequest>.SetRead((Reader r) => new CheatRequest
        {
            Action = r.ReadUInt8Unpacked(),
            Amount = r.ReadInt32(),
            Currency = r.ReadUInt8Unpacked(),
            Flag = r.ReadBoolean(),
            ItemId = r.ReadInt32(),
        });

        GenericWriter<CheatReply>.SetWrite((Writer w, CheatReply m) =>
        {
            w.WriteUInt8Unpacked(m.Action);
            w.WriteBoolean(m.Ok);
            w.WriteInt32(m.Value);
        });
        GenericReader<CheatReply>.SetRead((Reader r) => new CheatReply
        {
            Action = r.ReadUInt8Unpacked(),
            Ok = r.ReadBoolean(),
            Value = r.ReadInt32(),
        });
    }

    // ────────────────────────── client ──────────────────────────

    public static bool Send(CheatRequest req)
    {
        var cm = InstanceFinder.ClientManager;
        if (cm == null || !cm.Started)
            return false;
        cm.Broadcast(req, Channel.Reliable);
        return true;
    }

    private static void OnClientReply(CheatReply r, Channel channel)
    {
        var action = (CheatAction)r.Action;
        if (action == CheatAction.Hello)
        {
            if (r.Ok && !HostModOnline)
            {
                HostModOnline = true;
                ModCore.Controller?.ShowToast(L.T(
                    "已连接主机 mod,联机作弊可用",
                    "已連線主機 mod,連機作弊可用",
                    "Host mod connected — online cheats available"));
            }
            return;
        }

        var c = ModCore.Controller;
        if (c != null)
            c.ShowToast(r.Ok
                ? L.T("主机已执行", "主機已執行", "Executed on host")
                : L.T("主机执行失败(看主机日志)", "主機執行失敗(看主機日誌)", "Host failed (see host log)"));
    }

    // ────────────────────────── host ──────────────────────────

    private static void OnServerRequest(NetworkConnection conn, CheatRequest req, Channel channel)
    {
        var reply = new CheatReply { Action = req.Action };
        try
        {
            var player = FindPlayer(conn);
            int value = Execute((CheatAction)req.Action, req.Flag, req.Amount, req.Currency, req.ItemId, player);
            reply.Ok = true;
            reply.Value = value;
        }
        catch (Exception e)
        {
            reply.Ok = false;
            Loader.Log($"Online request failed (action={req.Action}): " + e);
        }

        var nm = InstanceFinder.NetworkManager;
        if (nm != null)
            nm.ServerManager.Broadcast(conn, reply, channel: Channel.Reliable);
    }

    private static Player FindPlayer(NetworkConnection conn)
    {
        if (conn == null)
            return null;
        foreach (var p in PlayerManager.Players)
        {
            if (p != null && p.Owner == conn)
                return p;
        }
        return null;
    }

    /// <summary>
    /// Executes one cheat action. Shared by the host-side broadcast handler and
    /// local direct calls (requester = local player).
    /// </summary>
    public static int Execute(CheatAction action, bool flag, int amount, byte currency, int itemId, Player requester)
    {
        switch (action)
        {
            case CheatAction.Hello:
                return 1;

            case CheatAction.AddMoney:
                if (requester == null) throw new Exception("requester not found");
                MoneyManager.AddMoney(amount, requester, (Currency)currency);
                return 0;

            case CheatAction.GodSelf:
                if (requester == null) throw new Exception("requester not found");
                if (flag) _godProtected.Add(requester.Owner);
                else _godProtected.Remove(requester.Owner);
                return _godProtected.Count;

            case CheatAction.KeepInventorySelf:
                if (requester == null) throw new Exception("requester not found");
                if (flag) _keepInvProtected.Add(requester.Owner);
                else _keepInvProtected.Remove(requester.Owner);
                return _keepInvProtected.Count;

            case CheatAction.BuiltInCheats:
                Features.SetBuiltInCheats(flag);
                return 0;

            case CheatAction.VitalsLock:
                if (requester == null) throw new Exception("requester not found");
                if (flag) _vitalsLock.Add(requester);
                else _vitalsLock.Remove(requester);
                return _vitalsLock.Count;

            case CheatAction.NextIsland:
                Features.NextIsland(flag);
                return 0;

            case CheatAction.KillBoss:
                Features.KillBoss();
                return 0;

            case CheatAction.KillAllCreatures:
                return Features.KillAllCreatures();

            case CheatAction.RespawnCreatures:
                if (requester == null) throw new Exception("requester not found");
                return Features.SpawnCreaturesAround(requester.Transform.position, 12);

            case CheatAction.FishRain:
                if (requester == null) throw new Exception("requester not found");
                return Features.FishRainAt(requester.Transform.position + Vector3.up * 9f, amount);

            case CheatAction.SpawnItem:
                if (requester == null) throw new Exception("requester not found");
                return Features.SpawnItemByIdAt((byte)itemId, requester.Transform.position, amount);

            case CheatAction.RouletteAutoWin:
                Features.RouletteAutoWin = flag;
                return 0;

            default:
                throw new Exception("unknown action " + action);
        }
    }
}
