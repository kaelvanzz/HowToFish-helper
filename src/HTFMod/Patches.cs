using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HTFMod;

/// <summary>无后座:吞掉视角后座(开枪视角抖动)。</summary>
[HarmonyPatch(typeof(PlayerCamera), "Recoil")]
internal static class Patch_PlayerCamera_Recoil
{
    private static bool Prefix() => !Features.NoRecoil;
}

/// <summary>无后座:吞掉枪模后座(枪身模型晃动)。</summary>
[HarmonyPatch(typeof(Weapon), "AddModelRecoil")]
internal static class Patch_Weapon_AddModelRecoil
{
    private static bool Prefix() => !Features.NoRecoil;
}

/// <summary>无后座:吞掉持械刚体的后座冲量。</summary>
[HarmonyPatch(typeof(PlayerToolMovement), "Recoil")]
internal static class Patch_PlayerToolMovement_Recoil
{
    private static bool Prefix() => !Features.NoRecoil;
}

/// <summary>无后座:冻结跳跃/下落时的枪模摆动(Fall 驱动的垂直甩动)。</summary>
[HarmonyPatch(typeof(PlayerToolMovement), "Fall")]
internal static class Patch_PlayerToolMovement_Fall
{
    private static readonly FieldInfo FiFallPos = AccessTools.Field(typeof(PlayerToolMovement), "_fallPos");

    private static bool Prefix(PlayerToolMovement __instance)
    {
        if (!Features.NoRecoil)
            return true;
        try { FiFallPos?.SetValue(__instance, Vector3.zero); }
        catch { }
        return false; // 跳过原逻辑,枪模空中不甩
    }
}

/// <summary>
/// 玩家飞行:每帧在移动逻辑写入速度后接管垂直分量(Move 是游戏写玩家速度的方法)。
/// Space 上升 / Ctrl 下降 / 松开悬停;开船时自动让位给载具飞行。
/// 同时清零重力累积量(_extraGravityForce),否则下一帧 Move 会先减一次重力导致缓慢下沉。
/// </summary>
[HarmonyPatch(typeof(PlayerMovement), "Move")]
internal static class Patch_PlayerMovement_Move
{
    private static readonly FieldInfo FiExtraGravity = AccessTools.Field(typeof(PlayerMovement), "_extraGravityForce");

    private static void Postfix(PlayerMovement __instance)
    {
        if (!Features.PlayerFlight)
            return;
        try
        {
            var p = Player.LocalPlayer;
            if (p == null || __instance != p.Movement)
                return;
            if (BoatManager.Boat != null && Boat.IsDrivingLocally)
                return; // 开船:让位给载具飞行

            FiExtraGravity?.SetValue(__instance, 0f); // 抵消下一帧的重力累积

            var rb = p.Rigidbody;
            if (rb == null || rb.isKinematic)
                return;

            Vector3 v = rb.linearVelocity;
            if (InputX.KeyHeld(KeyCode.Space))
                v.y = 10f;
            else if (InputX.KeyHeld(KeyCode.LeftControl))
                v.y = -10f;
            else
                v.y = 0f;
            rb.linearVelocity = v;
        }
        catch { }
    }
}

/// <summary>FOV 倍率:在游戏每帧设置视场角之后按倍率缩放。</summary>
[HarmonyPatch(typeof(PlayerCamera), "SetFov")]
internal static class Patch_PlayerCamera_SetFov
{
    private static void Postfix()
    {
        float m = Features.FovMult;
        if (m > 0.99f && m < 1.01f)
            return;
        try
        {
            var cam = GameInfo.CurCamera;
            if (cam != null)
                cam.fieldOfView = Mathf.Clamp(cam.fieldOfView * m, 20f, 150f);
        }
        catch { }
    }
}

/// <summary>第三人称:游戏每帧把相机放到角色头部之后,再沿视线方向拉远指定距离。</summary>
[HarmonyPatch(typeof(PlayerCamera), "SetCamPosRot")]
internal static class Patch_PlayerCamera_SetCamPosRot
{
    private static void Postfix(PlayerCamera __instance)
    {
        if (!Features.ThirdPerson || Features.ThirdPersonDist <= 0.05f)
            return;
        try
        {
            var t = __instance.CamTransform;
            if (t != null)
                t.position -= t.forward * Features.ThirdPersonDist;
        }
        catch { }
    }
}

/// <summary>
/// 上帝模式:伤害总入口拦截(着火/中毒/溺水/饥饿/攻击全走 TakeDamage)。
/// - 自己机器上的服务器判定 → 本地开关豁免;
/// - 联机桥登记过的玩家 → 主机侧豁免。
/// </summary>
[HarmonyPatch(typeof(PlayerVitals), "TakeDamage")]
internal static class Patch_PlayerVitals_TakeDamage
{
    private static bool Prefix(PlayerVitals __instance)
    {
        if (!Features.GodMode && !Multiplayer.HasGodProtected)
            return true;
        try
        {
            var owner = __instance.Owner;
            if (owner == null)
                return true;
            if (owner.IsLocalClient && Features.GodMode)
                return false;
            if (Multiplayer.IsGodProtected(owner))
                return false;
        }
        catch { }
        return true;
    }
}

/// <summary>
/// 上帝模式(补充):攻击命中入口同样拦截,顺带免去命中音效/击杀计分噪音。
/// </summary>
[HarmonyPatch(typeof(PlayerVitals), "LocalHit")]
internal static class Patch_PlayerVitals_LocalHit
{
    private static bool Prefix(PlayerVitals __instance)
    {
        if (!Features.GodMode && !Multiplayer.HasGodProtected)
            return true;
        try
        {
            var owner = __instance.Owner;
            if (owner == null)
                return true;
            if (owner.IsLocalClient && Features.GodMode)
                return false;
            if (Multiplayer.IsGodProtected(owner))
                return false;
        }
        catch { }
        return true;
    }
}

/// <summary>
/// 保留物品栏(只对自己生效):跳过死亡/复活时的全物品掉落。
/// 自己的掉落看本地开关;联机桥登记过的玩家看主机侧登记。
/// </summary>
[HarmonyPatch(typeof(PlayerInventory), "ServerDropAll")]
internal static class Patch_PlayerInventory_ServerDropAll
{
    private static bool Prefix(PlayerInventory __instance)
    {
        try
        {
            var owner = __instance.Owner;
            if (owner == null)
                return true;
            if (owner.IsLocalClient)
                return !Features.KeepInventory;
            return !Multiplayer.IsKeepInvProtected(owner);
        }
        catch { return true; }
    }
}

/// <summary>
/// 秒杀(对所有武器生效):所有伤害的必经口。
/// 枪械/投射物/近战刀/拳头的伤害最终都带着"攻击者"参数走这两个入口,
/// 攻击者是本地玩家 → 伤害 ×1000。其他人的攻击不受影响。
/// 注意:Item.LocalHit 是虚方法(Creature/BowheadWhale/DeadPlayer 都有重写),
/// 子类重写会在 HtfPlugin 启动时被逐个枚举并打上同样的补丁。
/// </summary>
internal static class Patch_Item_LocalHit
{
    public static void Prefix(Item __instance, Player player, ref int damage)
    {
        if (!Features.SelfOneShot || damage <= 0)
            return;
        if (player == null || player != Player.LocalPlayer)
            return;
        damage = 100000; // 固定高伤害:基础 3 的拳头也能一拳秒后期魔王
    }
}

[HarmonyPatch(typeof(PlayerVitals), "LocalHit")]
internal static class Patch_PlayerVitals_LocalHit_Damage
{
    private static void Prefix(PlayerVitals __instance, Player playerWhoHit, ref int damage)
    {
        if (!Features.SelfOneShot || damage <= 0)
            return;
        if (playerWhoHit == null || playerWhoHit != Player.LocalPlayer)
            return;
        damage = 100000; // 固定高伤害
    }
}

