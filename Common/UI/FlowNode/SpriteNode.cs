using System;
using System.Collections.Generic;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Leclair.Stardew.Common.UI.FlowNode {
	public struct SpriteNode : IFlowNode {
		public SpriteInfo Sprite { get; }
		public float Scale { get; }
		public Alignment Alignment { get; }

		public bool NoComponent { get; }
		public Func<IFlowNodeSlice, bool> OnClick { get; }
		public Func<IFlowNodeSlice, bool> OnHover { get; }

		public SpriteNode(SpriteInfo sprite, float scale, Alignment? alignment = null, Func<IFlowNodeSlice, bool> onClick = null, Func<IFlowNodeSlice, bool> onHover = null, bool noComponent = false) {
			Sprite = sprite;
			Scale = scale;
			Alignment = alignment ?? Alignment.None;
			OnClick = onClick;
			OnHover = onHover;
			NoComponent = noComponent;
		}

		public bool IsEmpty() {
			return Sprite == null || Scale <= 0;
		}

		public IFlowNodeSlice Slice(IFlowNodeSlice last, SpriteFont font, float maxWidth, float remaining) {
			if (last != null)
				return null;

			return new UnslicedNode(this, 16 * Scale, 16 * Scale, WrapMode.None);
		}

		public void Draw(IFlowNodeSlice slice, SpriteBatch batch, Vector2 position, float scale, SpriteFont defaultFont, Color? defaultColor, Color? defaultShadowColor, CachedFlowLine line, CachedFlow flow) {
			if (IsEmpty())
				return;

			Sprite.Draw(batch, position, scale * Scale);
		}

		public override bool Equals(object obj) {
			return obj is SpriteNode node &&
				   EqualityComparer<SpriteInfo>.Default.Equals(Sprite, node.Sprite) &&
				   Scale == node.Scale &&
				   Alignment == node.Alignment &&
				   NoComponent == node.NoComponent &&
				   EqualityComparer<Func<IFlowNodeSlice, bool>>.Default.Equals(OnClick, node.OnClick) &&
				   EqualityComparer<Func<IFlowNodeSlice, bool>>.Default.Equals(OnHover, node.OnHover);
		}

		public override int GetHashCode() {
			int hashCode = 2138745294;
			hashCode = hashCode * -1521134295 + EqualityComparer<SpriteInfo>.Default.GetHashCode(Sprite);
			hashCode = hashCode * -1521134295 + Scale.GetHashCode();
			hashCode = hashCode * -1521134295 + Alignment.GetHashCode();
			hashCode = hashCode * -1521134295 + NoComponent.GetHashCode();
			hashCode = hashCode * -1521134295 + EqualityComparer<Func<IFlowNodeSlice, bool>>.Default.GetHashCode(OnClick);
			hashCode = hashCode * -1521134295 + EqualityComparer<Func<IFlowNodeSlice, bool>>.Default.GetHashCode(OnHover);
			return hashCode;
		}
	}
}