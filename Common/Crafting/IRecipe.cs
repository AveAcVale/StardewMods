using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using StardewValley;

namespace Leclair.Stardew.Common.Crafting {
	public interface IRecipe {

		// Identity

		int SortValue { get; }

		string Name { get; }
		string DisplayName { get; }
		string Description { get; }

		int GetTimesCrafted(Farmer who);

		CraftingRecipe CraftingRecipe { get; }

		// Display

		Texture2D Texture { get; }
		Rectangle SourceRectangle { get; }

		int GridHeight { get; }
		int GridWidth { get; }

		// Cost

		int QuantityPerCraft { get; }

		IIngredient[] Ingredients { get; }

		// Creation

		bool Stackable { get; }

		Item CreateItem();

	}

	public interface IRecipeSprite {

		SpriteInfo Sprite { get; }

	}
}
