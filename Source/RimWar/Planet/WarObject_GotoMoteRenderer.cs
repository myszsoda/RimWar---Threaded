using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RimWar.Planet
{
    [StaticConstructorOnStartup]
    public class WarObject_GotoMoteRenderer
    {
        private PlanetTile tile;

        private float lastOrderedToTileTime = -0.51f;

        private static MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();

        private static Material cachedMaterial;

        public static readonly Material FeedbackGoto = MaterialPool.MatFrom("Things/Mote/FeedbackGoto", ShaderDatabase.WorldOverlayTransparent, WorldMaterials.DynamicObjectRenderQueue);

        public void RenderMote()
        {
            float num = (Time.time - lastOrderedToTileTime) / 0.5f;
            if (!(num > 1f))
            {
                if (cachedMaterial == null)
                {
                    cachedMaterial = MaterialPool.MatFrom((Texture2D)FeedbackGoto.mainTexture, FeedbackGoto.shader, Color.white, WorldMaterials.DynamicObjectRenderQueue);
                }
                WorldGrid worldGrid = Find.WorldGrid;
                Vector3 tileCenter = worldGrid.GetTileCenter(tile);
                Color value = new Color(1f, 1f, 1f, 1f - num);
                propertyBlock.SetColor(ShaderPropertyIDs.Color, value);
                Vector3 pos = tileCenter;
                float size = 0.8f * worldGrid.AverageTileSize;
                float altOffset = 0.018f;
                Material material = cachedMaterial;
                MaterialPropertyBlock materialPropertyBlock = propertyBlock;
                WorldRendererUtility.DrawQuadTangentialToPlanet(pos, size, altOffset, material, 0, false, false, materialPropertyBlock);
            }
        }

        public void OrderedToTile(PlanetTile tile)
        {
            this.tile = tile;
            lastOrderedToTileTime = Time.time;
        }
    }
}
