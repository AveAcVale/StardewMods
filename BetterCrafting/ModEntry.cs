using System;
using System.Collections.Generic;

using Leclair.Stardew.BetterCrafting.Managers;
using Leclair.Stardew.Common;
using Leclair.Stardew.Common.Events;
using Leclair.Stardew.Common.Integrations.GenericModConfigMenu;
using Leclair.Stardew.Common.Inventory;

using Microsoft.Xna.Framework.Graphics;

using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;

using StardewValley;
using StardewValley.Menus;
using StardewValley.Objects;

namespace Leclair.Stardew.BetterCrafting {

	public class ModEntry : ModSubscriber {

		public static ModEntry instance;
		public static ModAPI API;

		private readonly PerScreen<IClickableMenu> CurrentMenu = new();

		public ModConfig Config;

		public RecipeManager Recipes;
		public FavoriteManager Favorites;

		private GMCMIntegration<ModConfig, ModEntry> intGMCM;
		internal Integrations.RaisedGardenBeds.RGBIntegration intRGB;

		public ChestProvider ChestProvider = new(any: true);
		public Texture2D ButtonTexture;

		public override void Entry(IModHelper helper) {
			base.Entry(helper);

			instance = this;
			API = new ModAPI(this);

			// Read Config
			Config = Helper.ReadConfig<ModConfig>();

			// Init
			I18n.Init(Helper.Translation);

			Recipes = new RecipeManager(this);
			Favorites = new FavoriteManager(this);

			Sprites.Load(Helper.Content);
		}

		public override object GetApi() {
			return API;
		}


		#region Events

		[Subscriber]
		private void OnUpdateTicked(object sender, UpdateTickedEventArgs e) {
			IClickableMenu menu = Game1.activeClickableMenu;
			if (CurrentMenu.Value == menu)
				return;

			// Replace crafting pages.
			KeybindList list = new(SButton.LeftShift);
			if (list.IsDown()) {
				CurrentMenu.Value = menu;
				return;
			}

			if (menu is CraftingPage page) {
				bool cooking = CraftingPageHelper.IsCooking(page);
				if (cooking ? Config.ReplaceCooking : Config.ReplaceCrafting) {
					CommonHelper.YeetMenu(page);

					menu = Game1.activeClickableMenu = Menus.BetterCraftingPage.Open(
						this,
						Game1.player.currentLocation,
						null,
						cooking: cooking,
						standalone_menu: true,
						material_containers: page._materialContainers
					);
				}
			}

			// Replace crafting pages in the menu.
			if (menu is GameMenu gm && Config.ReplaceCrafting) {
				for (int i = 0; i < gm.pages.Count; i++) {
					if (gm.pages[i] is CraftingPage cp) {
						CommonHelper.YeetMenu(cp);

						gm.pages[i] = Menus.BetterCraftingPage.Open(
							this,
							Game1.player.currentLocation,
							null,
							width: gm.width,
							height: gm.height,
							cooking: false,
							standalone_menu: false
						);
					}
				}
			}

			CurrentMenu.Value = menu;
		}

		[Subscriber]
		private void OnGameLaunched(object sender, GameLaunchedEventArgs e) {
			// More Init
			RegisterSettings();

			// Integrations
			intRGB = new(this);

			// Load Data
			Recipes.LoadRecipes();
			Recipes.LoadDefaults();
		}

		#endregion

		#region Configuration

		public void SaveConfig() {
			Helper.WriteConfig(Config);
		}

		public bool HasGMCM() {
			return intGMCM?.IsLoaded ?? false;
		}

		public void OpenGMCM() {
			intGMCM?.OpenMenu();
		}


		private void RegisterSettings() {
			intGMCM = new GMCMIntegration<ModConfig, ModEntry>(this, () => Config, () => Config = new ModConfig(), () => SaveConfig());

			Dictionary<Models.SeasoningMode, string> seasoning = new();
			seasoning.Add(Models.SeasoningMode.Disabled, I18n.Seasoning_Disabled());
			seasoning.Add(Models.SeasoningMode.Enabled, I18n.Seasoning_Enabled());
			seasoning.Add(Models.SeasoningMode.InventoryOnly, I18n.Seasoning_Inventory());

			intGMCM.Register(true);
			intGMCM
				.AddLabel(I18n.Setting_General())
				.Add(I18n.Setting_ReplaceCrafting(), I18n.Setting_ReplaceCrafting_Tip(), c => c.ReplaceCrafting, (c, val) => c.ReplaceCrafting = val)
				.Add(I18n.Setting_ReplaceCooking(), I18n.Setting_ReplaceCooking_Tip(), c => c.ReplaceCooking, (c, val) => c.ReplaceCooking = val)
				.Add(I18n.Setting_EnableCategories(), I18n.Setting_EnableCategories_Tip(), c => c.UseCategories, (c, val) => c.UseCategories = val);

			intGMCM
				.AddLabel(I18n.Setting_Crafting(), I18n.Setting_Crafting_Tip())
				.Add(I18n.Setting_UniformGrid(), I18n.Setting_UniformGrid_Tip(), c => c.UseUniformGrid, (c, val) => c.UseUniformGrid = val)
				.Add(I18n.Setting_BigCraftablesLast(), I18n.Setting_BigCraftablesLast_Tip(), c => c.SortBigLast, (c, val) => c.SortBigLast = val);

			intGMCM
				.AddLabel(I18n.Setting_Cooking(), I18n.Setting_Cooking_Tip())
				.AddChoice(
					I18n.Setting_Seasoning(),
					I18n.Setting_Seasoning_Tip(),
					c => c.UseSeasoning,
					(c, val) => c.UseSeasoning = val,
					choices: seasoning
				)
				.Add(I18n.Setting_HideUnknown(), I18n.Setting_HideUnknown_Tip(), c => c.HideUnknown, (c, val) => c.HideUnknown = val);
		}

		#endregion

		public bool CanEnterNutDoor() {
			int num = Math.Max(Game1.netWorldState.Value.GoldenWalnutsFound.Value - 1, 0);
			return num >= 100;
		}

		public bool DoesTranslationExist(string key) {
			// SMAPI's translation API is very opaque.
			// But SMAPI's reflection helper is here to help with SMAPI.
			// Thank you, SMAPI.
			object Translator = Helper.Reflection.GetField<object>(Helper.Translation, "Translator", false).GetValue();
			IDictionary<string, Translation> ForLocale = Translator == null ? null : Helper.Reflection.GetField<IDictionary<string, Translation>>(Translator, "ForLocale", false).GetValue();
			return ForLocale != null && ForLocale.ContainsKey(key);
		}

		#region Providers

		public IInventoryProvider GetInventoryProvider(object obj) {
			// TODO: Check for MoveToConnected

			if (obj is Chest)
				return ChestProvider;

			return null;
		}

		#endregion

	}

}