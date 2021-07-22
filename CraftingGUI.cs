using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.Localization;
using Terraria.Map;
using Terraria.ModLoader;
using Terraria.UI;
using MagicStorage.Components;
using MagicStorage.Sorting;
using Terraria.GameContent;
using Terraria.Audio;
using MagicStorage.Items;

namespace MagicStorage
{
	public static class CraftingGUI
	{
		private const int padding = 4;
		private const int numColumns = 10;
		private const int numColumns2 = 7;
		private const float inventoryScale = 0.85f;
		private const float smallScale = 0.7f;

		public static MouseState curMouse;
		public static MouseState oldMouse;
		public static bool MouseClicked
		{
			get
			{
				return curMouse.LeftButton == ButtonState.Pressed && oldMouse.LeftButton == ButtonState.Released;
			}
		}

		private static UIPanel basePanel;
		private static float panelTop;
		private static float panelLeft;
		private static float panelWidth;
		private static float panelHeight;

		private static UIElement topBar;
		internal static UISearchBar searchBar;
		private static UIButtonChoice sortButtons;
		private static UIButtonChoice recipeButtons;
		private static UIElement topBar2;
		private static UIButtonChoice filterButtons;
		internal static UISearchBar searchBar2;

		private static UIText stationText;
		private static readonly UISlotZone stationZone = new UISlotZone(HoverStation, GetStation, inventoryScale);
		private static UIText recipeText;
		private static readonly UISlotZone recipeZone = new UISlotZone(HoverRecipe, GetRecipe, inventoryScale);

		internal static UIScrollbar scrollBar = new UIScrollbar();
		private static int scrollBarFocus = 0;
		private static int scrollBarFocusMouseStart;
		private static float scrollBarFocusPositionStart;
		private static readonly float scrollBarViewSize = 1f;
		private static float scrollBarMaxViewSize = 2f;

		private static readonly List<Item> items = new List<Item>();
		private static readonly Dictionary<int, int> itemCounts = new Dictionary<int, int>();
		private static bool[] adjTiles = new bool[TileLoader.TileCount];
		private static bool adjWater = false;
		private static bool adjLava = false;
		private static bool adjHoney = false;
		private static bool zoneSnow = false;
		private static bool alchemyTable = false;
		private static List<Recipe> recipes = new List<Recipe>();
		private static List<bool> recipeAvailable = new List<bool>();
		private static Recipe selectedRecipe = null;
		private static int numRows;
		private static int displayRows;
		private static bool slotFocus = false;

		private static readonly UIElement bottomBar = new UIElement();
		private static UIText capacityText;

		private static UIPanel recipePanel;
		private static float recipeTop;
		private static float recipeLeft;
		private static float recipeWidth;
		private static float recipeHeight;

		private static UIText recipePanelHeader;
		private static UIText ingredientText;
		private static readonly UISlotZone ingredientZone = new UISlotZone(HoverItem, GetIngredient, smallScale);
		private static UIText reqObjText;
		private static UIText reqObjText2;
		private static UIText storedItemsText;

		private static readonly UISlotZone storageZone = new UISlotZone(HoverStorage, GetStorage, smallScale);
		private static int numRows2;
		private static int displayRows2;
		private static readonly List<Item> storageItems = new List<Item>();
		private static readonly List<ItemData> blockStorageItems = new List<ItemData>();

		internal static UIScrollbar scrollBar2 = new UIScrollbar();
		private static readonly float scrollBar2ViewSize = 1f;
		private static float scrollBar2MaxViewSize = 2f;

		internal static UITextPanel<LocalizedText> craftButton;
		private static Item result = null;
		private static readonly UISlotZone resultZone = new UISlotZone(HoverResult, GetResult, inventoryScale);
		private static int craftTimer = 0;
		private const int startMaxCraftTimer = 20;
		private static int maxCraftTimer = startMaxCraftTimer;
		private static int rightClickTimer = 0;
		private const int startMaxRightClickTimer = 20;
		private static int maxRightClickTimer = startMaxRightClickTimer;

		private static readonly Object threadLock = new Object();
		private static readonly Object recipeLock = new Object();
		private static readonly Object itemsLock = new Object();
		private static bool threadRunning = false;
		internal static bool threadNeedsRestart = false;
		private static SortMode threadSortMode;
		private static FilterMode threadFilterMode;
		private static readonly List<Recipe> threadRecipes = new List<Recipe>();
		private static readonly List<bool> threadRecipeAvailable = new List<bool>();
		private static List<Recipe> nextRecipes = new List<Recipe>();
		private static List<bool> nextRecipeAvailable = new List<bool>();

		public static void Initialize()
		{
			lock (recipeLock)
			{
				recipes = nextRecipes;
				recipeAvailable = nextRecipeAvailable;
			}

			InitLangStuff();
			float itemSlotWidth = TextureAssets.InventoryBack.Value.Width * inventoryScale;
			float itemSlotHeight = TextureAssets.InventoryBack.Value.Height * inventoryScale;
			float smallSlotWidth = TextureAssets.InventoryBack.Value.Width * smallScale;
			float smallSlotHeight = TextureAssets.InventoryBack.Value.Height * smallScale;

			panelTop = Main.instance.invBottom + 60;
			panelLeft = 20f;
			basePanel = new UIPanel();
			float innerPanelWidth = numColumns * (itemSlotWidth + padding) + 20f + padding;
			panelWidth = basePanel.PaddingLeft + innerPanelWidth + basePanel.PaddingRight;
			panelHeight = Main.screenHeight - panelTop - 40f;
			basePanel.Left.Set(panelLeft, 0f);
			basePanel.Top.Set(panelTop, 0f);
			basePanel.Width.Set(panelWidth, 0f);
			basePanel.Height.Set(panelHeight, 0f);
			basePanel.Recalculate();

			recipePanel = new UIPanel();
			recipeTop = panelTop;
			recipeLeft = panelLeft + panelWidth;
			recipeWidth = numColumns2 * (smallSlotWidth + padding) + 20f + padding;
			recipeWidth += recipePanel.PaddingLeft + recipePanel.PaddingRight;
			recipeHeight = panelHeight;
			recipePanel.Left.Set(recipeLeft, 0f);
			recipePanel.Top.Set(recipeTop, 0f);
			recipePanel.Width.Set(recipeWidth, 0f);
			recipePanel.Height.Set(recipeHeight, 0f);
			recipePanel.Recalculate();

			topBar = new UIElement();
			topBar.Width.Set(0f, 1f);
			topBar.Height.Set(32f, 0f);
			basePanel.Append(topBar);

			InitSortButtons();
			topBar.Append(sortButtons);
			float sortButtonsRight = sortButtons.GetDimensions().Width + padding;
			InitRecipeButtons();
			float recipeButtonsLeft = sortButtonsRight + 32f + 3 * padding;
			recipeButtons.Left.Set(recipeButtonsLeft, 0f);
			topBar.Append(recipeButtons);
			float recipeButtonsRight = recipeButtonsLeft + recipeButtons.GetDimensions().Width + padding;

			searchBar.Left.Set(recipeButtonsRight + padding, 0f);
			searchBar.Width.Set(-recipeButtonsRight - 2 * padding, 1f);
			searchBar.Height.Set(0f, 1f);
			topBar.Append(searchBar);

			topBar2 = new UIElement();
			topBar2.Width.Set(0f, 1f);
			topBar2.Height.Set(32f, 0f);
			topBar2.Top.Set(36f, 0f);
			basePanel.Append(topBar2);

			InitFilterButtons();
			float filterButtonsRight = filterButtons.GetDimensions().Width + padding;
			topBar2.Append(filterButtons);
			searchBar2.Left.Set(filterButtonsRight + padding, 0f);
			searchBar2.Width.Set(-filterButtonsRight - 2 * padding, 1f);
			searchBar2.Height.Set(0f, 1f);
			topBar2.Append(searchBar2);

			stationText.Top.Set(76f, 0f);
			basePanel.Append(stationText);

			stationZone.Width.Set(0f, 1f);
			stationZone.Top.Set(100f, 0f);
			stationZone.Height.Set(70f, 0f);
			stationZone.SetDimensions(numColumns, 1);
			basePanel.Append(stationZone);

			recipeText.Top.Set(152f, 0f);
			basePanel.Append(recipeText);

			recipeZone.Width.Set(0f, 1f);
			recipeZone.Top.Set(176f, 0f);
			recipeZone.Height.Set(-216f, 1f);
			basePanel.Append(recipeZone);

			numRows = (recipes.Count + numColumns - 1) / numColumns;
			displayRows = (int)recipeZone.GetDimensions().Height / ((int)itemSlotHeight + padding);
			recipeZone.SetDimensions(numColumns, displayRows);
			int noDisplayRows = numRows - displayRows;
			if (noDisplayRows < 0)
			{
				noDisplayRows = 0;
			}
			scrollBarMaxViewSize = 1 + noDisplayRows;
			scrollBar.Height.Set(displayRows * (itemSlotHeight + padding), 0f);
			scrollBar.Left.Set(-20f, 1f);
			scrollBar.SetView(scrollBarViewSize, scrollBarMaxViewSize);
			recipeZone.Append(scrollBar);

			bottomBar.Width.Set(0f, 1f);
			bottomBar.Height.Set(32f, 0f);
			bottomBar.Top.Set(-32f, 1f);
			basePanel.Append(bottomBar);

			capacityText.Left.Set(6f, 0f);
			capacityText.Top.Set(6f, 0f);
			TEStorageHeart heart = GetHeart();
			int numItems = 0;
			int capacity = 0;
			if (heart != null)
			{
				foreach (TEAbstractStorageUnit abstractStorageUnit in heart.GetStorageUnits())
				{
					if(abstractStorageUnit is TEStorageUnit storageUnit) {
						numItems += storageUnit.NumItems;
						capacity += storageUnit.Capacity;
					}
				}
			}
			capacityText.SetText(numItems + "/" + capacity + " Items");
			bottomBar.Append(capacityText);

			recipePanel.Append(recipePanelHeader);
			ingredientText.Top.Set(30f, 0f);
			recipePanel.Append(ingredientText);

			ingredientZone.SetDimensions(numColumns2, 2);
			ingredientZone.Top.Set(54f, 0f);
			ingredientZone.Width.Set(0f, 1f);
			ingredientZone.Height.Set(60f, 0f);
			recipePanel.Append(ingredientZone);

			reqObjText.Top.Set(136f, 0f);
			recipePanel.Append(reqObjText);
			reqObjText2.Top.Set(160f, 0f);
			recipePanel.Append(reqObjText2);
			storedItemsText.Top.Set(190f, 0f);
			recipePanel.Append(storedItemsText);

			storageZone.Top.Set(214f, 0f);
			storageZone.Width.Set(0f, 1f);
			storageZone.Height.Set(-214f - 36, 1f);
			recipePanel.Append(storageZone);
			numRows2 = (storageItems.Count + numColumns2 - 1) / numColumns2;
			displayRows2 = (int)storageZone.GetDimensions().Height / ((int)smallSlotHeight + padding);
			storageZone.SetDimensions(numColumns2, displayRows2);
			int noDisplayRows2 = numRows2 - displayRows2;
			if (noDisplayRows2 < 0)
			{
				noDisplayRows2 = 0;
			}
			scrollBar2MaxViewSize = 1 + noDisplayRows2;
			scrollBar2.Height.Set(displayRows2 * (smallSlotHeight + padding), 0f);
			scrollBar2.Left.Set(-20f, 1f);
			scrollBar2.SetView(scrollBar2ViewSize, scrollBar2MaxViewSize);
			storageZone.Append(scrollBar2);

			craftButton.Top.Set(-32f, 1f);
			craftButton.Width.Set(100f, 0f);
			craftButton.Height.Set(24f, 0f);
			craftButton.PaddingTop = 8f;
			craftButton.PaddingBottom = 8f;
			recipePanel.Append(craftButton);

			resultZone.SetDimensions(1, 1);
			resultZone.Left.Set(-itemSlotWidth, 1f);
			resultZone.Top.Set(-itemSlotHeight, 1f);
			resultZone.Width.Set(itemSlotWidth, 0f);
			resultZone.Height.Set(itemSlotHeight, 0f);
			recipePanel.Append(resultZone);
		}

		private static void InitLangStuff()
		{
			if (searchBar == null)
			{
				searchBar = new UISearchBar(Language.GetText("Mods.MagicStorage.SearchName"));
			}
			if (searchBar2 == null)
			{
				searchBar2 = new UISearchBar(Language.GetText("Mods.MagicStorage.SearchMod"));
			}
			if (stationText == null)
			{
				stationText = new UIText(Language.GetText("Mods.MagicStorage.CraftingStations"));
			}
			if (recipeText == null)
			{
				recipeText = new UIText(Language.GetText("Mods.MagicStorage.Recipes"));
			}
			if (capacityText == null)
			{
				capacityText = new UIText("Items");
			}
			if (recipePanelHeader == null)
			{
				recipePanelHeader = new UIText(Language.GetText("Mods.MagicStorage.SelectedRecipe"));
			}
			if (ingredientText == null)
			{
				ingredientText = new UIText(Language.GetText("Mods.MagicStorage.Ingredients"));
			}
			if (reqObjText == null)
			{
				reqObjText = new UIText(Language.GetText("LegacyInterface.22"));
			}
			if (reqObjText2 == null)
			{
				reqObjText2 = new UIText("");
			}
			if (storedItemsText == null)
			{
				storedItemsText = new UIText(Language.GetText("Mods.MagicStorage.StoredItems"));
			}
			if (craftButton == null)
			{
				craftButton = new UITextPanel<LocalizedText>(Language.GetText("LegacyMisc.72"), 1f);
			}
		}

		internal static void Unload()
		{
			sortButtons = null;
			filterButtons = null;
			recipeButtons = null;
		}

		private static void InitSortButtons()
		{
			if (sortButtons == null)
			{
				sortButtons = new UIButtonChoice(new Texture2D[]
				{
					TextureAssets.InventorySort[0].Value,
					MagicStorage.Instance.Assets.Request<Texture2D>("SortID").Value,
					MagicStorage.Instance.Assets.Request<Texture2D>("SortName").Value
				},
				new LocalizedText[]
				{
					Language.GetText("Mods.MagicStorage.SortDefault"),
					Language.GetText("Mods.MagicStorage.SortID"),
					Language.GetText("Mods.MagicStorage.SortName")
				});
			}
		}

		private static void InitRecipeButtons()
		{
			if (recipeButtons == null)
			{
				recipeButtons = new UIButtonChoice(new Texture2D[]
				{
					MagicStorage.Instance.Assets.Request<Texture2D>("RecipeAvailable").Value,
					MagicStorage.Instance.Assets.Request<Texture2D>("RecipeAll").Value
				},
				new LocalizedText[]
				{
					Language.GetText("Mods.MagicStorage.RecipeAvailable"),
					Language.GetText("Mods.MagicStorage.RecipeAll")
				});
			}
		}

		private static void InitFilterButtons()
		{
			if (filterButtons == null)
			{
				filterButtons = new UIButtonChoice(new Texture2D[]
				{
					MagicStorage.Instance.Assets.Request<Texture2D>("FilterAll").Value,
					MagicStorage.Instance.Assets.Request<Texture2D>("FilterMelee").Value,
					MagicStorage.Instance.Assets.Request<Texture2D>("FilterPickaxe").Value,
					MagicStorage.Instance.Assets.Request<Texture2D>("FilterArmor").Value,
					MagicStorage.Instance.Assets.Request<Texture2D>("FilterPotion").Value,
					MagicStorage.Instance.Assets.Request<Texture2D>("FilterTile").Value,
					MagicStorage.Instance.Assets.Request<Texture2D>("FilterMisc").Value,
				},
				new LocalizedText[]
				{
					Language.GetText("Mods.MagicStorage.FilterAll"),
					Language.GetText("Mods.MagicStorage.FilterWeapons"),
					Language.GetText("Mods.MagicStorage.FilterTools"),
					Language.GetText("Mods.MagicStorage.FilterEquips"),
					Language.GetText("Mods.MagicStorage.FilterPotions"),
					Language.GetText("Mods.MagicStorage.FilterTiles"),
					Language.GetText("Mods.MagicStorage.FilterMisc")
				});
			}
		}

		public static void Update(GameTime gameTime)
		{try{
			oldMouse = StorageGUI.oldMouse;
			curMouse = StorageGUI.curMouse;
			if (Main.playerInventory && Main.player[Main.myPlayer].GetModPlayer<StoragePlayer>().ViewingStorage().X >= 0 && StoragePlayer.IsStorageCrafting())
			{
				if (curMouse.RightButton == ButtonState.Released)
				{
					ResetSlotFocus();
				}
				if (basePanel != null)
				{
					basePanel.Update(gameTime);
				}
				if (recipePanel != null)
				{
					recipePanel.Update(gameTime);
				}
				UpdateRecipeText();
				UpdateScrollBar();
				UpdateCraftButton();
			}
			else
			{
				scrollBarFocus = 0;
				selectedRecipe = null;
				craftTimer = 0;
				maxCraftTimer = startMaxCraftTimer;
				ResetSlotFocus();
			}}catch(Exception e){Main.NewTextMultiline(e.ToString());}
		}

		public static void Draw()
		{
			try
			{
				Player player = Main.player[Main.myPlayer];
				StoragePlayer modPlayer = player.GetModPlayer<StoragePlayer>();
				Initialize();
				if (Main.mouseX > panelLeft && Main.mouseX < recipeLeft + panelWidth && Main.mouseY > panelTop && Main.mouseY < panelTop + panelHeight)
				{
					player.mouseInterface = true;
					player.cursorItemIconEnabled = false;
					InterfaceHelper.HideItemIconCache();
				}
				basePanel.Draw(Main.spriteBatch);
				recipePanel.Draw(Main.spriteBatch);
				Vector2 pos = recipeZone.GetDimensions().Position();
				if (threadRunning)
				{
					Utils.DrawBorderString(Main.spriteBatch, "Loading", pos + new Vector2(8f, 8f), Color.White);
				}
				stationZone.DrawText();
				recipeZone.DrawText();
				ingredientZone.DrawText();
				storageZone.DrawText();
				resultZone.DrawText();
				sortButtons.DrawText();
				recipeButtons.DrawText();
				filterButtons.DrawText();
			}
			catch(Exception e)
			{
				Main.NewTextMultiline(e.ToString());
			}
		}

		private static Item GetStation(int slot, ref int context)
		{
			Item[] stations = GetCraftingStations();
			if (stations == null || slot >= stations.Length)
			{
				return new Item();
			}
			return stations[slot];
		}

		private static Item GetRecipe(int slot, ref int context)
		{
			if (threadRunning)
			{
				return new Item();
			}
			int index = slot + numColumns * (int)Math.Round(scrollBar.ViewPosition);
			Item item = index < recipes.Count ? recipes[index].createItem : new Item();
			if (item.IsAir && recipes[index] == selectedRecipe)
			{
				context = 6;
			}
			if (item.IsAir && !recipeAvailable[index])
			{
				context = recipes[index] == selectedRecipe ? 4 : 3;
			}
			return item;
		}

		private static Item GetIngredient(int slot, ref int context)
		{
			if (selectedRecipe == null || slot >= selectedRecipe.requiredItem.Count)
			{
				return new Item();
			}
			Item item = selectedRecipe.requiredItem[slot].Clone();
			if (selectedRecipe.HasRecipeGroup(RecipeGroupID.Wood) && item.type == ItemID.Wood)
			{
				item.SetNameOverride(Language.GetTextValue("LegacyMisc.37") + " " + Lang.GetItemNameValue(ItemID.Wood));
			}
			if (selectedRecipe.HasRecipeGroup(RecipeGroupID.Sand) && item.type == ItemID.SandBlock)
			{
				item.SetNameOverride(Language.GetTextValue("LegacyMisc.37") + " " + Lang.GetItemNameValue(ItemID.SandBlock));
			}
			if (selectedRecipe.HasRecipeGroup(RecipeGroupID.IronBar) && item.type == ItemID.IronBar)
			{
				item.SetNameOverride(Language.GetTextValue("LegacyMisc.37") + " " + Lang.GetItemNameValue(ItemID.IronBar));
			}
			if (selectedRecipe.HasRecipeGroup(RecipeGroupID.Fragment) && item.type == ItemID.FragmentSolar)
			{
				item.SetNameOverride(Language.GetTextValue("LegacyMisc.37") + " " + Language.GetTextValue("LegacyMisc.51"));
			}
			if (selectedRecipe.HasRecipeGroup(RecipeGroupID.PressurePlate) && item.type == ItemID.GrayPressurePlate)
			{
				item.SetNameOverride(Language.GetTextValue("LegacyMisc.37") + " " + Language.GetTextValue("LegacyMisc.38"));
			}
			if(ProcessGroupsForText(selectedRecipe, item.type, out string nameOverride)) {
				item.SetNameOverride(nameOverride);
			}
			return item;
		}

		internal static bool ProcessGroupsForText(Recipe recipe, int type, out string theText) {
			foreach (int num in recipe.acceptedGroups) {
				if (RecipeGroup.recipeGroups[num].ContainsItem(type)) {
					theText = RecipeGroup.recipeGroups[num].GetText();
					return true;
				}
			}

			theText = "";
			return false;
		}

		private static Item GetStorage(int slot, ref int context)
		{
			int index = slot + numColumns2 * (int)Math.Round(scrollBar2.ViewPosition);
			Item item = index < storageItems.Count ? storageItems[index] : new Item();
			if (blockStorageItems.Contains(new ItemData(item)))
			{
				context = 3;
			}
			return item;
		}

		private static Item GetResult(int slot, ref int context)
		{
			return slot == 0 && result != null ? result : new Item();
		}

		private static void UpdateRecipeText()
		{
			if (selectedRecipe == null)
			{
				reqObjText2.SetText("");
			}
			else
			{
				bool isEmpty = true;
				string text = "";
				for (int k = 0; k < selectedRecipe.requiredTile.Count; k++)
				{
					if (selectedRecipe.requiredTile[k] == -1)
					{
						break;
					}
					if (!isEmpty)
					{
						text += ", ";
					}
					text += Lang.GetMapObjectName(MapHelper.TileToLookup(selectedRecipe.requiredTile[k], 0));
					isEmpty = false;
				}
				if (selectedRecipe.HasCondition(Recipe.Condition.NearWater))
				{
					if (!isEmpty)
					{
						text += ", ";
					}
					text += Language.GetTextValue("LegacyInterface.53");
					isEmpty = false;
				}
				if (selectedRecipe.HasCondition(Recipe.Condition.NearHoney))
				{
					if (!isEmpty)
					{
						text += ", ";
					}
					text += Language.GetTextValue("LegacyInterface.58");
					isEmpty = false;
				}
				if (selectedRecipe.HasCondition(Recipe.Condition.NearLava))
				{
					if (!isEmpty)
					{
						text += ", ";
					}
					text += Language.GetTextValue("LegacyInterface.56");
					isEmpty = false;
				}
				if (selectedRecipe.HasCondition(Recipe.Condition.InSnow))
				{
					if (!isEmpty)
					{
						text += ", ";
					}
					text += Language.GetTextValue("LegacyInterface.123");
					isEmpty = false;
				}
				if (isEmpty)
				{
					text = Language.GetTextValue("LegacyInterface.23");
				}
				reqObjText2.SetText(text);
			}
		}

		private static void UpdateScrollBar()
		{
			if (slotFocus)
			{
				scrollBarFocus = 0;
				return;
			}
			Rectangle dim = scrollBar.GetClippingRectangle(Main.spriteBatch);
			Vector2 boxPos = new Vector2(dim.X, dim.Y + dim.Height * (scrollBar.ViewPosition / scrollBarMaxViewSize));
			float boxWidth = 20f * Main.UIScale;
			float boxHeight = dim.Height * (scrollBarViewSize / scrollBarMaxViewSize);
			Rectangle dim2 = scrollBar2.GetClippingRectangle(Main.spriteBatch);
			Vector2 box2Pos = new Vector2(dim2.X, dim2.Y + dim2.Height * (scrollBar2.ViewPosition / scrollBar2MaxViewSize));
			float box2Height = dim2.Height * (scrollBar2ViewSize / scrollBar2MaxViewSize);
			if (scrollBarFocus > 0)
			{
				if (curMouse.LeftButton == ButtonState.Released)
				{
					scrollBarFocus = 0;
				}
				else
				{
					int difference = curMouse.Y - scrollBarFocusMouseStart;
					if (scrollBarFocus == 1)
					{
						scrollBar.ViewPosition = scrollBarFocusPositionStart + (float)difference / boxHeight;
					}
					else if (scrollBarFocus == 2)
					{
						scrollBar2.ViewPosition = scrollBarFocusPositionStart + (float)difference / box2Height;
					}
				}
			}
			else if (MouseClicked)
			{
				if (curMouse.X > boxPos.X && curMouse.X < boxPos.X + boxWidth && curMouse.Y > boxPos.Y - 3f && curMouse.Y < boxPos.Y + boxHeight + 4f)
				{
					scrollBarFocus = 1;
					scrollBarFocusMouseStart = curMouse.Y;
					scrollBarFocusPositionStart = scrollBar.ViewPosition;
				}
				else if (curMouse.X > box2Pos.X && curMouse.X < box2Pos.X + boxWidth && curMouse.Y > box2Pos.Y - 3f && curMouse.Y < box2Pos.Y + box2Height + 4f)
				{
					scrollBarFocus = 2;
					scrollBarFocusMouseStart = curMouse.Y;
					scrollBarFocusPositionStart = scrollBar2.ViewPosition;
				}
			}
			if (scrollBarFocus == 0)
			{
				int difference = oldMouse.ScrollWheelValue / 250 - curMouse.ScrollWheelValue / 250;
				scrollBar.ViewPosition += difference;
			}
		}

		private static void UpdateCraftButton()
		{
			Rectangle dim = InterfaceHelper.GetFullRectangle(craftButton);
			bool flag = false;
			if (curMouse.X > dim.X && curMouse.X < dim.X + dim.Width && curMouse.Y > dim.Y && curMouse.Y < dim.Y + dim.Height)
			{
				craftButton.BackgroundColor = new Color(73, 94, 171);
				if (curMouse.LeftButton == ButtonState.Pressed)
				{
					if (selectedRecipe != null && IsAvailable(selectedRecipe) && PassesBlock(selectedRecipe))
					{
						if (craftTimer <= 0)
						{
							craftTimer = maxCraftTimer;
							maxCraftTimer = maxCraftTimer * 3 / 4;
							if (maxCraftTimer <= 0)
							{
								maxCraftTimer = 1;
							}
							TryCraft();
							RefreshItems();
							SoundEngine.PlaySound(7, -1, -1, 1);
						}
						craftTimer--;
						flag = true;
					}
				}
			}
			else
			{
				craftButton.BackgroundColor = new Color(63, 82, 151) * 0.7f;
			}
			if (selectedRecipe == null || !IsAvailable(selectedRecipe) || !PassesBlock(selectedRecipe))
			{
				craftButton.BackgroundColor = new Color(30, 40, 100) * 0.7f;
			}
			if (!flag)
			{
				craftTimer = 0;
				maxCraftTimer = startMaxCraftTimer;
			}
		}

		private static TEStorageHeart GetHeart()
		{
			Player player = Main.player[Main.myPlayer];
			StoragePlayer modPlayer = player.GetModPlayer<StoragePlayer>();
			return modPlayer.GetStorageHeart();
		}

		private static TECraftingAccess GetCraftingEntity()
		{
			Player player = Main.player[Main.myPlayer];
			StoragePlayer modPlayer = player.GetModPlayer<StoragePlayer>();
			return modPlayer.GetCraftingAccess();
		}

		private static Item[] GetCraftingStations()
		{
			TECraftingAccess ent = GetCraftingEntity();
			return ent?.stations;
		}

		public static void RefreshItems()
		{
			SortMode sortMode;
			FilterMode filterMode;

			lock (itemsLock)
			{
				items.Clear();
				TEStorageHeart heart = GetHeart();
				if (heart == null)
				{
					return;
				}
				items.AddRange(ItemSorter.SortAndFilter(heart.GetStoredItems(), SortMode.Id, FilterMode.All, "", ""));
				AnalyzeIngredients();
				InitLangStuff();
				InitSortButtons();
				InitRecipeButtons();
				InitFilterButtons();

				sortMode = sortButtons.Choice switch {
					0 => SortMode.Default,
					1 => SortMode.Id,
					2 => SortMode.Name,
					_ => SortMode.Default,
				};

				filterMode = filterButtons.Choice switch {
					0 => FilterMode.All,
					1 => FilterMode.Weapons,
					2 => FilterMode.Tools,
					3 => FilterMode.Equipment,
					4 => FilterMode.Potions,
					5 => FilterMode.Placeables,
					6 => FilterMode.Misc,
					_ => FilterMode.All,
				};

				RefreshStorageItems();
			}

			lock (threadLock)
			{
				threadNeedsRestart = true;
				threadSortMode = sortMode;
				threadFilterMode = filterMode;
				if (!threadRunning)
				{
					threadRunning = true;
					Thread thread = new Thread(RefreshRecipes);
					thread.Start();
				}
			}
		}

		private static void RefreshRecipes()
		{
			while (true)
			{try{
				SortMode sortMode;
				FilterMode filterMode;
				lock (threadLock)
				{
					threadNeedsRestart = false;
					sortMode = threadSortMode;
					filterMode = threadFilterMode;
				}
				var temp = ItemSorter.GetRecipes(sortMode, filterMode, searchBar2.Text, searchBar.Text);
				threadRecipes.Clear();
				threadRecipeAvailable.Clear();
				try
				{
					if (recipeButtons.Choice == 0)
					{
						threadRecipes.AddRange(temp.Where(recipe => IsAvailable(recipe)));
						threadRecipeAvailable.AddRange(threadRecipes.Select(recipe => true));
					}
					else
					{
						threadRecipes.AddRange(temp);
						threadRecipeAvailable.AddRange(threadRecipes.Select(recipe => IsAvailable(recipe)));
					}
				}
				catch (InvalidOperationException)
				{
				}
				catch (KeyNotFoundException)
				{
				}
				lock (recipeLock)
				{
					nextRecipes = new List<Recipe>();
					nextRecipeAvailable = new List<bool>();
					nextRecipes.AddRange(threadRecipes);
					nextRecipeAvailable.AddRange(threadRecipeAvailable);
					
				}
				lock (threadLock)
				{
					if (!threadNeedsRestart)
					{
						threadRunning = false;
						return;
					}
				}}catch(Exception e){Main.NewTextMultiline(e.ToString());}
			}
		}

		private static void AnalyzeIngredients()
		{
			Player player = Main.player[Main.myPlayer];

			lock (itemsLock)
			{
				itemCounts.Clear();
				if (adjTiles.Length != player.adjTile.Length)
				{
					Array.Resize(ref adjTiles, player.adjTile.Length);
				}
				for (int k = 0; k < adjTiles.Length; k++)
				{
					adjTiles[k] = false;
				}
				adjWater = false;
				adjLava = false;
				adjHoney = false;
				zoneSnow = false;
				alchemyTable = false;

				foreach (Item item in items)
				{
					if (itemCounts.ContainsKey(item.netID))
					{
						itemCounts[item.netID] += item.stack;
					}
					else
					{
						itemCounts[item.netID] = item.stack;
					}
				}
			}

			foreach (Item item in GetCraftingStations())
			{
				if (item.createTile >= TileID.Dirt)
				{
					adjTiles[item.createTile] = true;
					if (item.createTile == TileID.GlassKiln || item.createTile == TileID.Hellforge || item.createTile == TileID.AdamantiteForge)
					{
						adjTiles[TileID.Furnaces] = true;
					}
					if (item.createTile == TileID.AdamantiteForge)
					{
						adjTiles[TileID.Hellforge] = true;
					}
					if (item.createTile == TileID.MythrilAnvil)
					{
						adjTiles[TileID.Anvils] = true;
					}
					if (item.createTile == TileID.BewitchingTable || item.createTile == TileID.Tables2)
					{
						adjTiles[TileID.Tables] = true;
					}
					if (item.createTile == TileID.AlchemyTable)
					{
						adjTiles[TileID.Bottles] = true;
						adjTiles[TileID.Tables] = true;
						alchemyTable = true;
					}
					bool[] oldAdjTile = player.adjTile;
					bool oldAdjWater = adjWater;
					bool oldAdjLava = adjLava;
					bool oldAdjHoney = adjHoney;
					bool oldAlchemyTable = alchemyTable;
					player.adjTile = adjTiles;
					player.adjWater = false;
					player.adjLava = false;
					player.adjHoney = false;
					player.alchemyTable = false;
					TileLoader.AdjTiles(player, item.createTile);
					if (player.adjWater)
					{
						adjWater = true;
					}
					if (player.adjLava)
					{
						adjLava = true;
					}
					if (player.adjHoney)
					{
						adjHoney = true;
					}
					if (player.alchemyTable)
					{
						alchemyTable = true;
					}
					player.adjTile = oldAdjTile;
					player.adjWater = oldAdjWater;
					player.adjLava = oldAdjLava;
					player.adjHoney = oldAdjHoney;
					player.alchemyTable = oldAlchemyTable;
				}
				if (item.type == ItemID.WaterBucket || item.type == ItemID.BottomlessBucket)
				{
					adjWater = true;
				}
				if (item.type == ItemID.LavaBucket)
				{
					adjLava = true;
				}
				if (item.type == ItemID.HoneyBucket)
				{
					adjHoney = true;
				}
				if (item.type == ModContent.ItemType<SnowBiomeEmulator>())
				{
					zoneSnow = true;
				}
			}
			adjTiles[ModContent.ItemType<Items.CraftingAccess>()] = true;
		}

		private static bool IsAvailable(Recipe recipe)
		{
			foreach (int tile in recipe.requiredTile)
			{
				if (tile == -1)
				{
					break;
				}
				if (!adjTiles[tile])
				{
					return false;
				}
			}

			lock (itemsLock)
			{
				foreach (Item ingredient in recipe.requiredItem)
				{
					if (ingredient.type == ItemID.None)
					{
						break;
					}
					int stack = ingredient.stack;
					bool useRecipeGroup = false;
					foreach (int type in itemCounts.Keys)
					{
						if (RecipeGroupMatch(recipe, type, ingredient.type))
						{
							stack -= itemCounts[type];
							useRecipeGroup = true;
						}
					}
					if (!useRecipeGroup && itemCounts.ContainsKey(ingredient.netID))
					{
						stack -= itemCounts[ingredient.netID];
					}
					if (stack > 0)
					{
						return false;
					}
				}
			}

			if (recipe.HasCondition(Recipe.Condition.NearWater) && !adjWater && !adjTiles[TileID.Sinks])
			{
				return false;
			}
			if (recipe.HasCondition(Recipe.Condition.NearLava) && !adjLava)
			{
				return false;
			}
			if (recipe.HasCondition(Recipe.Condition.NearHoney) && !adjHoney)
			{
				return false;
			}
			if (recipe.HasCondition(Recipe.Condition.InSnow) && !zoneSnow)
			{
				return false;
			}
			try
			{
				BlockRecipes.active = false;
				if (!RecipeLoader.RecipeAvailable(recipe))
				{
					return false;
				}
			}
			finally
			{
				BlockRecipes.active = true;
			}
			return true;
		}

		private static bool PassesBlock(Recipe recipe)
		{
			foreach (Item ingredient in recipe.requiredItem)
			{
				if (ingredient.type == ItemID.None)
				{
					break;
				}
				int stack = ingredient.stack;
				bool useRecipeGroup = false;
				foreach (Item item in storageItems)
				{
					ItemData data = new ItemData(item);
					if (!blockStorageItems.Contains(data) && RecipeGroupMatch(recipe, item.netID, ingredient.type))
					{
						stack -= item.stack;
						useRecipeGroup = true;
					}
				}
				if (!useRecipeGroup)
				{
					foreach (Item item in storageItems)
					{
						ItemData data = new ItemData(item);
						if (!blockStorageItems.Contains(data) && item.netID == ingredient.netID)
						{
							stack -= item.stack;
						}
					}
				}
				if (stack > 0)
				{
					return false;
				}
			}
			return true;
		}

		private static void RefreshStorageItems()
		{
			storageItems.Clear();
			result = null;
			if (selectedRecipe != null)
			{
				foreach (Item item in items)
				{
					for (int k = 0; k < selectedRecipe.requiredItem.Count; k++)
					{
						if (selectedRecipe.requiredItem[k].type == ItemID.None)
						{
							break;
						}
						if (item.type == selectedRecipe.requiredItem[k].type || RecipeGroupMatch(selectedRecipe, selectedRecipe.requiredItem[k].type, item.type))
						{
							storageItems.Add(item);
						}
					}
					if (item.type == selectedRecipe.createItem.type)
					{
						result = item;
					}
				}
				if (result == null)
				{
					result = new Item();
					result.SetDefaults(selectedRecipe.createItem.type);
					result.stack = 0;
				}
			}
		}

		private static bool RecipeGroupMatch(Recipe recipe, int type1, int type2)
		{
			//Why in the hell were the "recipe.useX" methods privated in 1.4.  I'm not happy about this.
			// - absoluteAquarian
			return CanUse(RecipeGroupID.Wood, type1, type2)
				|| CanUse(RecipeGroupID.Sand, type1, type2)
				|| CanUse(RecipeGroupID.IronBar, type1, type2)
				|| CanUse(RecipeGroupID.Fragment, type1, type2)
				|| CanUse(RecipeGroupID.PressurePlate, type1, type2)
				|| CanUse(RecipeGroupID.Birds, type1, type2)
				|| CanUse(RecipeGroupID.Bugs, type1, type2)
				|| CanUse(RecipeGroupID.Butterflies, type1, type2)
				|| CanUse(RecipeGroupID.Dragonflies, type1, type2)
				|| CanUse(RecipeGroupID.Ducks, type1, type2)
				|| CanUse(RecipeGroupID.Fireflies, type1, type2)
				|| CanUse(RecipeGroupID.FishForDinner, type1, type2)
				|| CanUse(RecipeGroupID.Fruit, type1, type2)
				|| CanUse(RecipeGroupID.GoldenCritter, type1, type2)
				|| CanUse(RecipeGroupID.Scorpions, type1, type2)
				|| CanUse(RecipeGroupID.Snails, type1, type2)
				|| CanUse(RecipeGroupID.Turtles, type1, type2)
				|| AcceptedByItemGroups(recipe, type1, type2);

			//return recipe.useWood(type1, type2) || recipe.useSand(type1, type2) || recipe.useIronBar(type1, type2) || recipe.useFragment(type1, type2) || recipe.AcceptedByItemGroups(type1, type2) || recipe.usePressurePlate(type1, type2);
		}

		private static bool CanUse(int groupID, int inventoryType, int requiredType)
			=> RecipeGroup.recipeGroups[groupID].ContainsItem(requiredType) && RecipeGroup.recipeGroups[groupID].ContainsItem(inventoryType);

		internal static bool AcceptedByItemGroups(Recipe recipe, int invType, int reqType) {
			foreach (int num in recipe.acceptedGroups) {
				if (RecipeGroup.recipeGroups[num].ContainsItem(invType) && RecipeGroup.recipeGroups[num].ContainsItem(reqType))
					return true;
			}
			
			return false;
		}

		private static void HoverStation(int slot, ref int hoverSlot)
		{
			TECraftingAccess ent = GetCraftingEntity();
			if (ent == null || slot >= ent.stations.Length)
			{
				return;
			}

			Player player = Main.player[Main.myPlayer];
			if (MouseClicked)
			{
				bool changed = false;
				if (!ent.stations[slot].IsAir && ItemSlot.ShiftInUse)
				{
					Item result = player.GetItem(Main.myPlayer, DoWithdraw(slot), GetItemSettings.InventoryEntityToPlayerInventorySettings);
					if (!result.IsAir && Main.mouseItem.IsAir)
					{
						Main.mouseItem = result;
						result = new Item();
					}
					if (!result.IsAir && Main.mouseItem.type == result.type && Main.mouseItem.stack < Main.mouseItem.maxStack)
					{
						Main.mouseItem.stack += result.stack;
						result = new Item();
					}
					if (!result.IsAir)
					{
						player.QuickSpawnClonedItem(result);
					}
					changed = true;
				}
				else if (player.itemAnimation == 0 && player.itemTime == 0)
				{
					int oldType = Main.mouseItem.type;
					int oldStack = Main.mouseItem.stack;
					Main.mouseItem = DoStationSwap(Main.mouseItem, slot);
					if (oldType != Main.mouseItem.type || oldStack != Main.mouseItem.stack)
					{
						changed = true;
					}
				}
				if (changed)
				{
					RefreshItems();
					SoundEngine.PlaySound(7, -1, -1, 1);
				}
			}

			hoverSlot = slot;
		}

		private static void HoverRecipe(int slot, ref int hoverSlot)
		{
			int visualSlot = slot;
			slot += numColumns * (int)Math.Round(scrollBar.ViewPosition);
			if (slot < recipes.Count)
			{
				if (MouseClicked)
				{
					selectedRecipe = recipes[slot];
					
					lock (itemsLock)
					{
						RefreshStorageItems();
					}
					
					blockStorageItems.Clear();
				}
				hoverSlot = visualSlot;
			}
		}

		private static void HoverItem(int slot, ref int hoverSlot)
		{
			hoverSlot = slot;
		}

		private static void HoverStorage(int slot, ref int hoverSlot)
		{
			int visualSlot = slot;
			slot += numColumns2 * (int)Math.Round(scrollBar2.ViewPosition);
			if (slot < storageItems.Count)
			{
				if (MouseClicked)
				{
					ItemData data = new ItemData(storageItems[slot]);
					if (blockStorageItems.Contains(data))
					{
						blockStorageItems.Remove(data);
					}
					else
					{
						blockStorageItems.Add(data);
					}
				}
				hoverSlot = visualSlot;
			}
		}

		private static void HoverResult(int slot, ref int hoverSlot)
		{
			if (slot != 0)
			{
				return;
			}

			Player player = Main.player[Main.myPlayer];
			if (MouseClicked)
			{
				bool changed = false;
				if (!Main.mouseItem.IsAir && player.itemAnimation == 0 && player.itemTime == 0 && result != null && Main.mouseItem.type == result.type)
				{
					if (TryDepositResult(Main.mouseItem))
					{
						changed = true;
					}
				}
				else if (Main.mouseItem.IsAir && result != null && !result.IsAir)
				{
					Item toWithdraw = result.Clone();
					if (toWithdraw.stack > toWithdraw.maxStack)
					{
						toWithdraw.stack = toWithdraw.maxStack;
					}
					Main.mouseItem = DoWithdrawResult(toWithdraw, ItemSlot.ShiftInUse);
					if (ItemSlot.ShiftInUse)
					{
						Main.mouseItem = player.GetItem(Main.myPlayer, Main.mouseItem, GetItemSettings.InventoryEntityToPlayerInventorySettings);
					}
					changed = true;
				}
				if (changed)
				{
					RefreshItems();
					SoundEngine.PlaySound(7, -1, -1, 1);
				}
			}

			if (curMouse.RightButton == ButtonState.Pressed && oldMouse.RightButton == ButtonState.Released && result != null && !result.IsAir && (Main.mouseItem.IsAir || ItemData.Matches(Main.mouseItem, items[slot]) && Main.mouseItem.stack < Main.mouseItem.maxStack))
			{
				slotFocus = true;
			}

			hoverSlot = slot;

			if (slotFocus)
			{
				SlotFocusLogic();
			}
		}

		private static void SlotFocusLogic()
		{
			if (result == null || result.IsAir || (!Main.mouseItem.IsAir && (!ItemData.Matches(Main.mouseItem, result) || Main.mouseItem.stack >= Main.mouseItem.maxStack)))
			{
				ResetSlotFocus();
			}
			else
			{
				if (rightClickTimer <= 0)
				{
					rightClickTimer = maxRightClickTimer;
					maxRightClickTimer = maxRightClickTimer * 3 / 4;
					if (maxRightClickTimer <= 0)
					{
						maxRightClickTimer = 1;
					}
					Item toWithdraw = result.Clone();
					toWithdraw.stack = 1;
					Item withdrawn = DoWithdrawResult(toWithdraw);
					if (Main.mouseItem.IsAir)
					{
						Main.mouseItem = withdrawn;
					}
					else
					{
						Main.mouseItem.stack += withdrawn.stack;
					}
					SoundEngine.LegacySoundPlayer.SoundInstanceMenuTick.Stop();
					SoundEngine.LegacySoundPlayer.SoundInstanceMenuTick = SoundEngine.LegacySoundPlayer.SoundMenuTick.Value.CreateInstance();
					SoundEngine.PlaySound(12, -1, -1, 1);
					RefreshItems();
				}
				rightClickTimer--;
			}
		}

		private static void ResetSlotFocus()
		{
			slotFocus = false;
			rightClickTimer = 0;
			maxRightClickTimer = startMaxRightClickTimer;
		}

		private static Item DoWithdraw(int slot)
		{
			TECraftingAccess access = GetCraftingEntity();
			if (Main.netMode == NetmodeID.SinglePlayer)
			{
				Item result = access.TryWithdrawStation(slot);
				RefreshItems();
				return result;
			}
			else
			{
				NetHelper.SendWithdrawStation(access.ID, slot);
				return new Item();
			}
		}

		private static Item DoStationSwap(Item item, int slot)
		{
			TECraftingAccess access = GetCraftingEntity();
			if (Main.netMode == NetmodeID.SinglePlayer)
			{
				Item result = access.DoStationSwap(item, slot);
				RefreshItems();
				return result;
			}
			else
			{
				NetHelper.SendStationSlotClick(access.ID, item, slot);
				return new Item();
			}
		}

		private static void TryCraft()
		{
			List<Item> toWithdraw;
			lock (itemsLock)
			{
				List<Item> availableItems = new List<Item>(storageItems.Where(item => !blockStorageItems.Contains(new ItemData(item))).Select(item => item.Clone()));
				toWithdraw = new List<Item>();
				for (int k = 0; k < selectedRecipe.requiredItem.Count; k++)
				{
					Item item = selectedRecipe.requiredItem[k];
					if (item.type == ItemID.None)
					{
						break;
					}
					int stack = item.stack;
					Recipe Recipe = selectedRecipe;
					if (Recipe != null)
					{
						Recipe.AddConsumeItemCallback((Recipe recipe, int type, ref int amount) => stack = amount);
					}
					if (selectedRecipe.HasTile(TileID.AlchemyTable) && alchemyTable)
					{
						int save = 0;
						for (int j = 0; j < stack; j++)
						{
							if (Main.rand.Next(3) == 0)
							{
								save++;
							}
						}
						stack -= save;
					}
					if (stack > 0)
					{
						foreach (Item tryItem in availableItems)
						{
							if (item.type == tryItem.type || RecipeGroupMatch(selectedRecipe, item.type, tryItem.type))
							{
								if (tryItem.stack > stack)
								{
									Item temp = tryItem.Clone();
									temp.stack = stack;
									toWithdraw.Add(temp);
									tryItem.stack -= stack;
									stack = 0;
								}
								else
								{
									toWithdraw.Add(tryItem.Clone());
									stack -= tryItem.stack;
									tryItem.stack = 0;
									tryItem.type = ItemID.None;
								}
							}
						}
					}
				}
			}

			Item resultItem = selectedRecipe.createItem.Clone();
			resultItem.Prefix(-1);

			RecipeLoader.OnCraft(resultItem, selectedRecipe);

			if (Main.netMode == NetmodeID.SinglePlayer)
			{
				foreach (Item item in DoCraft(GetHeart(), toWithdraw, resultItem))
				{
					Main.player[Main.myPlayer].QuickSpawnClonedItem(item, item.stack);
				}
			}
			else if (Main.netMode == NetmodeID.MultiplayerClient)
			{
				NetHelper.SendCraftRequest(GetHeart().ID, toWithdraw, resultItem);
			}
		}

		internal static List<Item> DoCraft(TEStorageHeart heart, List<Item> toWithdraw, Item result)
		{
			List<Item> items = new List<Item>();
			foreach (Item tryWithdraw in toWithdraw)
			{
				Item withdrawn = heart.TryWithdraw(tryWithdraw);
				if (!withdrawn.IsAir)
				{
					items.Add(withdrawn);
				}
				if (withdrawn.stack < tryWithdraw.stack)
				{
					for (int k = 0; k < items.Count; k++)
					{
						heart.DepositItem(items[k]);
						if (items[k].IsAir)
						{
							items.RemoveAt(k);
							k--;
						}
					}
					return items;
				}
			}
			items.Clear();
			heart.DepositItem(result);
			if (!result.IsAir)
			{
				items.Add(result);
			}
			return items;
		}

		private static bool TryDepositResult(Item item)
		{
			int oldStack = item.stack;
			DoDepositResult(item);
			return oldStack != item.stack;
		}

		private static void DoDepositResult(Item item)
		{
			TEStorageHeart heart = GetHeart();
			if (Main.netMode == NetmodeID.SinglePlayer)
			{
				heart.DepositItem(item);
			}
			else
			{
				NetHelper.SendDeposit(heart.ID, item);
				item.SetDefaults(0, true);
			}
		}

		private static Item DoWithdrawResult(Item item, bool toInventory = false)
		{
			TEStorageHeart heart = GetHeart();
			if (Main.netMode == NetmodeID.SinglePlayer)
			{
				return heart.TryWithdraw(item);
			}
			else
			{
				NetHelper.SendWithdraw(heart.ID, item, toInventory);
				return new Item();
			}
		}
	}
}