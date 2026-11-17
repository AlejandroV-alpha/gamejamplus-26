using GameJam.Inventory;
using GameJam.Multiplayer;
using GameJam.UI;
using UnityEngine;

namespace GameJam.Combat
{
    /// <summary>
    /// HUD local del arma. Reune el estado de <see cref="Gun"/> y lo dibuja con
    /// <see cref="AmmoHudRenderer"/>: tarjeta de municion, anillo de recarga y aviso sin balas.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GunAmmoHud : MonoBehaviour
    {
        private Gun gun;
        private PlayerEquipment equipment;
        private MarsFusionAvatar avatar;
        private GameJam.Inventory.Inventory inventory;
        private float visible, lastAmmo = -1, lastShotTime = -10f, reloadDoneTime = -10f;
        private bool wasReloading;

        private void Awake()
        {
            gun = GetComponent<Gun>();
            equipment = GetComponent<PlayerEquipment>();
            avatar = GetComponent<MarsFusionAvatar>();
            inventory = GetComponent<GameJam.Inventory.Inventory>();
        }

        private bool Active => gun != null && gun.Object != null && gun.Object.IsValid && gun.HasStateAuthority
            && equipment != null && equipment.IsWeaponEquipped && avatar != null && avatar.IsAlive && !avatar.TabletOpen;

        private void OnGUI()
        {
            if (gun == null || gun.Object == null || !gun.Object.IsValid || !gun.HasStateAuthority) return;
            bool active = Active;
            if (Event.current.type == EventType.Repaint)
            {
                visible = Mathf.MoveTowards(visible, active ? 1f : 0f, Time.unscaledDeltaTime * 6f);
                bool reloading = active && gun.IsReloading;
                if (wasReloading && !reloading && active) reloadDoneTime = Time.unscaledTime;
                wasReloading = reloading;
                int ammoNow = active ? gun.GetCurrentAmmo() : -1;
                if (ammoNow >= 0 && lastAmmo >= 0 && ammoNow < lastAmmo) lastShotTime = Time.unscaledTime;
                lastAmmo = ammoNow;
            }
            if (visible <= .01f) return;

            var item = inventory != null ? inventory.SelectedItem : null;
            var state = new AmmoHudRenderer.State
            {
                Ammo = Mathf.Max(0, gun.GetCurrentAmmo()),
                Magazine = gun.MagazineSize,
                Reloading = gun.IsReloading,
                ReloadProgress = gun.ReloadProgress,
                ReloadRemaining = gun.ReloadRemaining,
                SinceDryFire = Time.unscaledTime - gun.LastDryFireTime,
                SinceReloadDone = Time.unscaledTime - reloadDoneTime,
                SinceShot = Time.unscaledTime - lastShotTime,
                WeaponIcon = item != null && item.icon != null ? item.icon.texture : null,
                WeaponName = "Pistola",
                Alpha = visible
            };
            float s = InteractionPromptCard.Scale;
            Rect safe = Screen.safeArea;
            AmmoHudRenderer.DrawCounter(new Vector2(safe.xMax - 28 * s, Screen.height - safe.yMin - 170 * s), state);
        }
    }
}
