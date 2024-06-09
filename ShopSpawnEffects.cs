using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Utils;
using Newtonsoft.Json.Linq;
using ShopAPI;

namespace ShopSpawnEffects
{
    public class ShopSpawnEffects : BasePlugin
    {
        public override string ModuleName => "[SHOP] Spawn Effects";
        public override string ModuleDescription => "";
        public override string ModuleAuthor => "E!N";
        public override string ModuleVersion => "v1.0";

        private IShopApi? SHOP_API;
        private const string CategoryName = "SpawnEffects";
        public static JObject? JsonSpawnEffects { get; private set; }
        private readonly PlayerSpawnEffect[] playerSpawnEffects = new PlayerSpawnEffect[65];

        public override void OnAllPluginsLoaded(bool hotReload)
        {
            SHOP_API = IShopApi.Capability.Get();
            if (SHOP_API == null) return;

            LoadConfig();
            InitializeShopItems();
            SetupTimersAndListeners();
        }

        private void LoadConfig()
        {
            string configPath = Path.Combine(ModuleDirectory, "../../configs/plugins/Shop/SpawnEffects.json");
            if (File.Exists(configPath))
            {
                JsonSpawnEffects = JObject.Parse(File.ReadAllText(configPath));
            }
        }

        private void InitializeShopItems()
        {
            if (JsonSpawnEffects == null || SHOP_API == null) return;

            SHOP_API.CreateCategory(CategoryName, "Ёффекты при спавне");

            foreach (var item in JsonSpawnEffects!.Properties())
            {
                Task.Run(async () =>
                {
                    int itemId = await SHOP_API.AddItem(item.Name, (string)item.Value["name"]!, CategoryName, (int)item.Value["price"]!, (int)item.Value["sellprice"]!, (int)item.Value["duration"]!);
                    SHOP_API.SetItemCallbacks(itemId, OnClientBuyItem, OnClientSellItem, OnClientToggleItem);
                }).Wait();
            }
        }

        private void SetupTimersAndListeners()
        {
            RegisterListener<Listeners.OnClientDisconnect>(playerSlot => playerSpawnEffects[playerSlot] = null!);
        }

        public void OnClientBuyItem(CCSPlayerController player, int itemId, string categoryName, string uniqueName, int buyPrice, int sellPrice, int duration, int count)
        {
            playerSpawnEffects[player.Slot] = new PlayerSpawnEffect(itemId);
        }

        [GameEventHandler]
        public HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
        {
            var player = @event.Userid;
            if (player != null && !player.IsBot && playerSpawnEffects[player.Slot] != null)
            {
                SpawnExplosion(player);
            }
            return HookResult.Continue;
        }

        public static void SpawnExplosion(CCSPlayerController player)
        {
            if (player.Pawn.Value == null) return;

            var pawn = player.Pawn.Value;
            var heProjectile = Utilities.CreateEntityByName<CHEGrenadeProjectile>("hegrenade_projectile");
            if (heProjectile == null || !heProjectile.IsValid) return;

            var node = pawn.CBodyComponent?.SceneNode;
            if (node == null) return;

            Vector pos = node.AbsOrigin;
            pos.Z += 10;

            heProjectile.TicksAtZeroVelocity = 100;
            heProjectile.TeamNum = pawn.TeamNum;
            heProjectile.Damage = 0;
            heProjectile.DmgRadius = 0;
            heProjectile.Teleport(pos, node.AbsRotation, new Vector(0, 0, -10));
            heProjectile.DispatchSpawn();
            heProjectile.AcceptInput("InitializeSpawnFromWorld", player.PlayerPawn.Value, player.PlayerPawn.Value, "");
            heProjectile.DetonateTime = 0;
        }

        public void OnClientToggleItem(CCSPlayerController player, int itemId, string uniqueName, int state)
        {
            if (state == 1)
            {
                playerSpawnEffects[player.Slot] = new PlayerSpawnEffect(itemId);
            }
            else if (state == 0)
            {
                OnClientSellItem(player, itemId, uniqueName, 0);
            }
        }

        public void OnClientSellItem(CCSPlayerController player, int itemId, string uniqueName, int sellPrice)
        {
            playerSpawnEffects[player.Slot] = null!;
        }

        public record PlayerSpawnEffect(int ItemID);
    }
}