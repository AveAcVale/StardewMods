using System;
using System.Collections.Generic;
using System.Linq;

using Leclair.Stardew.Almanac.Menus;
using Leclair.Stardew.Almanac.Pages;
using Leclair.Stardew.Common;
using Leclair.Stardew.Common.Types;
using Leclair.Stardew.Common.UI;
using Leclair.Stardew.Common.UI.FlowNode;
using Leclair.Stardew.Common.UI.SimpleLayout;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

using StardewModdingAPI.Utilities;

using StardewValley;
using StardewValley.Menus;

using SObject = StardewValley.Object;

namespace Leclair.Stardew.Almanac.Crops {
	public class CropPage : BasePage, ICalendarPage, ITab {

		private static Tuple<int, double>[] FERTILIZERS = new Tuple<int, double>[] {
			new(465, 0.10),
			new(466, 0.25),
			new(918, 0.33)
		};

		private List<CropInfo>[] LastDays;

		private CropInfo? HoverCrop;

		private IEnumerable<IFlowNode> Flow;

		// Fertilizer and Agriculturist Status
		public List<ClickableComponent> FertComponents;
		private readonly List<Tuple<Item, SpriteInfo, double, int>> Fertilizers;
		private int FertIndex = -1;
		private Tuple<Item, SpriteInfo, double, int> Fertilizer => Fertilizers == null || FertIndex < 0 || FertIndex >= Fertilizers.Count ? null : Fertilizers[FertIndex];

		private bool PaddyBonus = true;
		public ClickableComponent tabPaddy;
		private SpriteInfo spritePaddy;
		private int tabPaddySprite;

		private bool Agriculturist;
		public ClickableComponent tabAgri;
		private SpriteInfo spriteAgri;
		private int tabAgriSprite;

		private Cache<SpriteInfo[], CropInfo?> CropGrowth;

		private Dictionary<CropInfo, IFlowNode> CropNodes = new();

		private WorldDate HoveredDate;
		private Cache<ISimpleNode, WorldDate> CalendarTip;

		private WorldDate ClickedDate;
		private int ClickedIndex;

		#region Lifecycle

		public static CropPage GetPage(AlmanacMenu menu, ModEntry mod) {
			if (!mod.Config.ShowCrops)
				return null;

			return new(menu, mod);
		}

		public CropPage(AlmanacMenu menu, ModEntry mod) : base(menu, mod) {
			// Caches
			CalendarTip = new(date => {
				if (date == null)
					return null;

				List<CropInfo> crops = LastDays == null ? null : LastDays[date.DayOfMonth - 1];

				if (crops == null)
					return null;

				SimpleBuilder builder = new();

				builder.Text(I18n.Crop_LastDay());
				builder.Divider();

				foreach (CropInfo crop in crops)
					builder.Sprite(crop.Sprite, 3f, label: crop.Name);

				return builder.GetLayout();

			}, () => HoveredDate);

			CropGrowth = new(key => {
				if (!key.HasValue)
					return null;

				CropInfo crop = key.Value;

				SpriteInfo[] sprites = new SpriteInfo[WorldDate.DaysPerMonth];
				int[] phases = GetActualPhaseTime(crop.Days, crop.Phases, crop.IsPaddyCrop);

				int phase = 0;
				int days = 0;
				bool grown = false;

				for (int i = 0; i < sprites.Length; i++) {
					if (grown) {
						if (crop.Regrow > 0) {
							days++;
							if (days > crop.Regrow)
								days = 1;

							if (days == 1)
								sprites[i] = Mod.Config.PreviewUseHarvestSprite ? crop.Sprite : crop.PhaseSprites[crop.PhaseSprites.Length - 2];
							else
								sprites[i] = crop.PhaseSprites[crop.PhaseSprites.Length - 1];

							continue;

						} else {
							grown = false;
							phase = 0;
							days = 1;

							sprites[i] = Mod.Config.PreviewUseHarvestSprite ? crop.Sprite : crop.PhaseSprites[crop.PhaseSprites.Length - 1];
							continue;
						}

					} else {
						if (!Mod.Config.PreviewPlantOnFirst && Game1.Date.SeasonIndex == Menu.Season && Game1.Date.DayOfMonth > (i + 1)) {
							sprites[i] = null;
							continue;
						}

						while (true) {
							days++;
							if (days > phases[phase]) {
								phase++;
								days = 0;
								if (phase >= phases.Length) {
									grown = true;
									break;
								}
							} else
								break;
						}
						if (grown) {
							i--;
							continue;
						}
					}

					sprites[i] = crop.PhaseSprites[phase];
				}

				return sprites;

			}, () => HoverCrop);

			// Cache Fertilizer items.
			Fertilizers = new(FERTILIZERS.Length);
			FertComponents = new(FERTILIZERS.Length);

			for (int i = 0; i < FERTILIZERS.Length; i++) {
				int id = FERTILIZERS[i].Item1;

				SObject obj = id == -1 ? null : new(FERTILIZERS[i].Item1, 1);
				Item item = obj?.getOne();
				SpriteInfo sprite = item == null ? null : SpriteHelper.GetSprite(item, Mod.Helper);

				Fertilizers.Add(new(item, sprite, FERTILIZERS[i].Item2, Game1.random.Next(2 * AlmanacMenu.TABS.Length)));
				FertComponents.Add(new(new Rectangle(0, 0, 64, 64), (string) null) {
					myID = 5000 + i,
					upNeighborID = ClickableComponent.SNAP_AUTOMATIC,
					rightNeighborID = ClickableComponent.SNAP_AUTOMATIC,
					leftNeighborID = ClickableComponent.SNAP_AUTOMATIC
				});
			}

			tabAgri = new(new(0, 0, 64, 64), (string) null) {
				myID = 4999,
				upNeighborID = ClickableComponent.SNAP_AUTOMATIC,
				leftNeighborID = ClickableComponent.SNAP_AUTOMATIC,
				rightNeighborID = ClickableComponent.SNAP_AUTOMATIC
			};
			tabAgriSprite = Game1.random.Next(2 * AlmanacMenu.TABS.Length);
			spriteAgri = new SpriteInfo(Game1.mouseCursors, new Rectangle(80, 624, 16, 16));

			tabPaddy = new(new(0, 0, 64, 64), (string) null) {
				myID = 4998,
				upNeighborID = ClickableComponent.SNAP_AUTOMATIC,
				leftNeighborID = ClickableComponent.SNAP_AUTOMATIC,
				rightNeighborID = ClickableComponent.SNAP_AUTOMATIC
			};
			tabPaddySprite = Game1.random.Next(2 * AlmanacMenu.TABS.Length);
			spritePaddy = new(Menu.background, new Rectangle(118, 330, 16, 16));

			// Cache Agriculturist status.
			Agriculturist = Game1.player.professions.Contains(Farmer.agriculturist);

			Update();
		}

		#endregion

		#region Logic

		public static string AgriculturistName() {
			return LevelUpMenu.getProfessionTitleFromNumber(Farmer.agriculturist);
		}

		public int[] GetActualPhaseTime(int days, int[] phases, bool isPaddyCrop) {
			float modifier = (float) (Fertilizer?.Item3 ?? 0f);
			if (PaddyBonus && isPaddyCrop)
				modifier += 0.25f;
			if (Agriculturist)
				modifier += 0.1f;

			int remove = (int) Math.Ceiling(days * modifier);
			int tries = 0;

			int[] result = (int[]) phases.Clone();

			while (remove > 0 && tries < 3) {
				for (int i = 0; i < result.Length; i++) {
					if ((i > 0 || result[i] > 1) && result[i] != 99999) {
						result[i]--;
						remove--;
					}
					if (remove <= 0)
						break;
				}
				tries++;
			}

			return result;
		}

		public void Update() {

			LastDays = new List<CropInfo>[WorldDate.DaysPerMonth];

			CropNodes.Clear();

			List<CropInfo> crops = Mod.Crops.GetSeasonCrops(Menu.Season);
			crops.Sort((a,b) => StringComparer.CurrentCultureIgnoreCase.Compare(a.Name, b.Name));

			FlowBuilder builder = new();

			var agriculturist = Agriculturist ? FlowHelper.Builder()
				.Sprite(spriteAgri, 2)
				.Text($" {AgriculturistName()}", bold: true, color: Color.ForestGreen)
				.Build() : null;

			var fertilizer = Fertilizer == null || Fertilizer.Item1 == null ? null : FlowHelper.Builder()
				.Sprite(Fertilizer.Item2, 2)
				.Text($" {Fertilizer.Item1.DisplayName}", bold: true)
				.Build();


			if (Agriculturist && fertilizer != null)
				builder.Translate(Mod.Helper.Translation.Get("crop.using-both"), new { agriculturist, fertilizer });
			else if (Agriculturist)
				builder.Translate(Mod.Helper.Translation.Get("crop.using-agri"), new { agriculturist });
			else if (fertilizer != null)
				builder.Translate(Mod.Helper.Translation.Get("crop.using-speed"), new { fertilizer });
			else
				builder.Text(I18n.Crop_UsingNone());

			if (PaddyBonus)
				builder.Text($" {I18n.Crop_UsingPaddy()}");

			builder.Text("\n\n");

			WorldDate start = new(Menu.Date);
			start.Year = 1;

			WorldDate end = new(start);
			start.DayOfMonth = 1;
			end.DayOfMonth = WorldDate.DaysPerMonth;

			foreach (CropInfo crop in crops) {
				WorldDate last = new(crop.EndDate);

				int[] phases = GetActualPhaseTime(crop.Days, crop.Phases, crop.IsPaddyCrop);
				int days = phases.Sum();
				last.TotalDays -= days;

				if (last.SeasonIndex == Menu.Season) {
					int day = last.DayOfMonth;
					if (LastDays[day - 1] == null)
						LastDays[day - 1] = new();

					LastDays[day - 1].Add(crop);
				}

				bool OnHover(IFlowNodeSlice slice) {
					HoverCrop = crop;
					Menu.HoveredItem = crop.Item;
					return true;
				}

				SDate sdate = new(last.DayOfMonth, last.Season);

				IFlowNode node = new Common.UI.FlowNode.SpriteNode(crop.Sprite, 3f, onHover: OnHover);
				CropNodes[crop] = node;

				builder
					.Add(node)
					.Text($" {crop.Name}\n", font: Game1.dialogueFont, align: Alignment.Middle, onHover: OnHover, noComponent: true)
					.Text(I18n.Crop_GrowTime(count: days), shadow: false);

				if (crop.Regrow > 0)
					builder.Text($" {I18n.Crop_RegrowTime(count: crop.Regrow)}", shadow: false);

				if (crop.IsTrellisCrop)
					builder.Text($" {I18n.Crop_TrellisNote()}", shadow: false);

				if (crop.IsPaddyCrop)
					builder.Text($" {I18n.Crop_PaddyNote()}", shadow: false);

				if (crop.IsGiantCrop)
					builder.Text($" {I18n.Crop_GiantNote()}", shadow: false);

				builder
					.Text($" {I18n.Crop_LastDate(date: sdate.ToLocaleString(withYear: false))}", shadow: false)
					.Text("\n\n");
			}

			Flow = builder.Build();

			if (Active) {
				CropGrowth.Invalidate();
				Menu.SetFlow(Flow, 2);
			}
		}

		#endregion

		#region ITab

		public override int SortKey => 0;
		public override string TabSimpleTooltip => I18n.Page_Crops();
		public override Texture2D TabTexture => Game1.objectSpriteSheet;
		public override Rectangle? TabSource => Game1.getSourceRectForStandardTileSheet(Game1.objectSpriteSheet, 24, 16, 16);

		#endregion

		#region IAlmanacPage

		public override void Activate() {
			base.Activate();
			Menu.SetFlow(Flow, 2);
		}

		public override void DateChanged(WorldDate old, WorldDate newDate) {
			Update();
		}

		public override void UpdateComponents() {
			base.UpdateComponents();

			int x = Menu.xPositionOnScreen + Menu.width / 2 + 64;
			int y = Menu.yPositionOnScreen + Menu.height - 20;

			foreach (ClickableComponent cmp in FertComponents) {
				cmp.bounds = new(x, y, 64, 64);
				x += 68;
			}

			x += 36;
			tabAgri.bounds = new(x, y, 64, 64);

			x += 68;
			tabPaddy.bounds = new(x, y, 64, 64);
		}

		public override bool ReceiveKeyPress(Keys key) {

			if (key == Keys.OemTilde) {
				Agriculturist = !Agriculturist;
				Update();
				Game1.playSound("smallSelect");
				return true;
			}

			int idx = -1;

			if (key == Keys.D1)
				idx = 0;
			else if (key == Keys.D2)
				idx = 1;
			else if (key == Keys.D3)
				idx = 2;

			if (idx != -1) {
				FertIndex = FertIndex == idx ? -1 : idx;
				Update();
				Game1.playSound("smallSelect");
				return true;
			}

			return base.ReceiveKeyPress(key);
		}

		public override void PerformHover(int x, int y) {
			base.PerformHover(x, y);

			HoverCrop = null;

			for (int i = 0; i < FertComponents.Count; i++) {
				ClickableComponent cmp = FertComponents[i];
				if (cmp.containsPoint(x, y)) {
					Menu.HoverText = Fertilizers[i].Item1?.DisplayName ?? "???";
					return;
				}
			}

			if (tabAgri.containsPoint(x, y)) {
				Menu.HoverText = I18n.Crop_Toggle(AgriculturistName());
				return;
			}

			if (tabPaddy.containsPoint(x, y)) {
				Menu.HoverText = I18n.Crop_Toggle(I18n.Crop_Paddy());
				return;
			}
		}

		public override bool ReceiveLeftClick(int x, int y, bool playSound) {
			for (int i = 0; i < FertComponents.Count; i++) {
				ClickableComponent cmp = FertComponents[i];
				if (cmp.containsPoint(x, y)) {
					FertIndex = FertIndex == i ? -1 : i;
					Update();
					if (playSound)
						Game1.playSound("smallSelect");

					return true;
				}
			}

			if (tabAgri.containsPoint(x, y)) {
				Agriculturist = !Agriculturist;
				Update();
				if (playSound)
					Game1.playSound("smallSelect");

				return true;
			}

			if (tabPaddy.containsPoint(x, y)) {
				PaddyBonus = !PaddyBonus;
				Update();
				if (playSound)
					Game1.playSound("smallSelect");
			}

			return base.ReceiveLeftClick(x, y, playSound);
		}

		public override void Draw(SpriteBatch b) {
			base.Draw(b);

			for (int i = 0; i < FertComponents.Count; i++) {
				ClickableComponent cmp = FertComponents[i];
				SpriteInfo sprite = Fertilizers[i].Item2;

				DrawTab(
					b,
					sprite,
					cmp.bounds.X,
					cmp.bounds.Y + (FertIndex == i ? -8 : 0),
					Fertilizers[i].Item4
				);
			}

			// Agriculturist Tab
			DrawTab(
				b,
				spriteAgri,
				tabAgri.bounds.X,
				tabAgri.bounds.Y + (Agriculturist ? -8 : 0),
				tabAgriSprite
			);

			// Paddy Tab
			DrawTab(
				b,
				spritePaddy,
				tabPaddy.bounds.X,
				tabPaddy.bounds.Y + (PaddyBonus ? -8 : 0),
				tabPaddySprite
			);
		}

		private void DrawTab(SpriteBatch b, SpriteInfo sprite, int x, int y, int index) {
			bool reflect = false;
			if (index >= AlmanacMenu.TABS.Length) {
				index -= AlmanacMenu.TABS.Length;
				reflect = true;
			}

			// Tab Background
			b.Draw(
				Menu.background,
				new Vector2(x, y),
				AlmanacMenu.TABS[0][index],
				Color.White,
				1 * (float) Math.PI / 2f,
				new Vector2(0, 16),
				4f,
				reflect ? SpriteEffects.FlipVertically : SpriteEffects.None,
				0.0001f
			);

			// Tab Texture
			sprite?.Draw(b, new Vector2(x + 16, y + 12), 2f);
		}

		#endregion

		#region ICalendarPage

		public bool ShouldDimPastCells => true;
		public bool ShouldHighlightToday => true;

		public void DrawUnderCell(SpriteBatch b, WorldDate date, Rectangle bounds) {
			SpriteInfo[] sprites = CropGrowth.Value;
			if (sprites != null) {
				SpriteInfo sprite = sprites[date.DayOfMonth - 1];
				if (sprite == null)
					return;

				bool tall = sprite.BaseSource.Height > sprite.BaseSource.Width;
				int size = (tall ? 32 : 16) * 3;

				sprite.Draw(
					b,
					new Vector2(
						bounds.X + (bounds.Width - size) / 2,
						bounds.Y + (bounds.Height - size) - (tall ? 0 : 8)
					),
					tall ? 6f : 3f
				);

				return;
			}

			List<CropInfo> crops = LastDays == null ? null : LastDays[date.DayOfMonth - 1];

			if (crops == null)
				return;

			int row = 0;
			int col = 0;
			float scale = 2;

			int padX = 2;
			int padY = 4;

			int rows, cols;
			if (crops.Count <= 2) {
				rows = crops.Count;
				cols = 1;
			} else if (crops.Count <= 4) {
				rows = cols = 2;
			} else if (crops.Count <= 6) {
				cols = 2;
				rows = Math.Min(3, (int) Math.Ceiling(crops.Count / 2f));
				if (rows > 2)
					padY = 1;
				scale = 2;
			} else if (crops.Count <= 15) {
				cols = 3;
				rows = Math.Min(5, (int) Math.Ceiling(crops.Count / 3f));
				padX = 4;
				padY = 8;
				if (rows > 4)
					padY = 3;
				scale = 1;
			} else {
				cols = 4;
				rows = Math.Min(5, (int) Math.Ceiling(crops.Count / 4f));
				padY = 8;
				if (rows > 4)
					padY = 3;
				scale = 1;
			}

			int width = (int) (16 * scale) * cols + (cols > 1 ? (int) (padX * scale) * cols - 1 : 0);
			int height = (int) (16 * scale) * rows + (rows > 1 ? (int) (padY * scale) * rows - 1 : 0);

			int offsetX = bounds.X + (bounds.Width - width) / 2;
			int offsetY = bounds.Y + 24 + (bounds.Height - height - 24) / 2;

			foreach (CropInfo crop in crops) {
				crop.Sprite?.Draw(
					b,
					new Vector2(
						offsetX + ((16 + padX) * scale) * col,
						offsetY + ((16 + padY) * scale) * row
					),
					scale
				);

				col++;
				if (col >= cols) {
					col = 0;
					row++;
					if (row >= rows)
						break;
				}
			}
		}

		public void DrawOverCell(SpriteBatch b, WorldDate date, Rectangle bounds) {

		}

		public bool ReceiveCellLeftClick(int x, int y, WorldDate date, Rectangle bounds) {
			List<CropInfo> crops = LastDays == null ? null : LastDays[date.DayOfMonth - 1];

			if (crops == null || crops.Count == 0)
				return false;

			// If we're using gamepad controls, loop through every crop in the tile.
			if (Game1.options.gamepadControls) {
				if (date != ClickedDate) {
					ClickedDate = date;
					ClickedIndex = -1;
				}

				ClickedIndex++;
				if (ClickedIndex >= crops.Count)
					ClickedIndex = 0;

				if (CropNodes.TryGetValue(crops[ClickedIndex], out IFlowNode node))
					if (Menu.ScrollFlow(node))
						Game1.playSound("shiny4");

				return true;
			}

			// If we're using mouse controls, determine which crop is at that exact
			// position and scroll there.
			int row = 0;
			int col = 0;
			float scale = 2;

			int padX = 2;
			int padY = 4;

			int rows, cols;
			if (crops.Count <= 2) {
				rows = crops.Count;
				cols = 1;
			} else if (crops.Count <= 4) {
				rows = cols = 2;
			} else if (crops.Count <= 6) {
				cols = 2;
				rows = Math.Min(3, (int) Math.Ceiling(crops.Count / 2f));
				if (rows > 2)
					padY = 1;
				scale = 2;
			} else if (crops.Count <= 15) {
				cols = 3;
				rows = Math.Min(5, (int) Math.Ceiling(crops.Count / 3f));
				padX = 4;
				padY = 8;
				if (rows > 4)
					padY = 3;
				scale = 1;
			} else {
				cols = 4;
				rows = Math.Min(5, (int) Math.Ceiling(crops.Count / 4f));
				padY = 8;
				if (rows > 4)
					padY = 3;
				scale = 1;
			}

			int width = (int) (16 * scale) * cols + (cols > 1 ? (int) (padX * scale) * cols - 1 : 0);
			int height = (int) (16 * scale) * rows + (rows > 1 ? (int) (padY * scale) * rows - 1 : 0);

			int offsetX = (bounds.Width - width) / 2;
			int offsetY = 24 + (bounds.Height - height - 24) / 2;

			foreach (CropInfo crop in crops) {
				int startX = offsetX + (int) ((16 + padX) * scale) * col;
				int startY = offsetY + (int) ((16 + padY) * scale) * row;

				int endX = startX + (int) (16 * scale);
				int endY = startY + (int) (16 * scale);

				if (x >= startX && x <= endX && y >= startY && y <= endY) {
					if (CropNodes.TryGetValue(crop, out IFlowNode node)) {
						if (Menu.ScrollFlow(node))
							Game1.playSound("shiny4");
					}

					return true;
				}

				col++;
				if (col >= cols) {
					col = 0;
					row++;
					if (row >= rows)
						break;
				}
			}

			return false;
		}

		public bool ReceiveCellRightClick(int x, int y, WorldDate date, Rectangle bounds) {
			return false;
		}

		public void PerformCellHover(int x, int y, WorldDate date, Rectangle bounds) {
			HoveredDate = date;
			Menu.HoverNode = CalendarTip.Value;
		}

		#endregion

	}
}