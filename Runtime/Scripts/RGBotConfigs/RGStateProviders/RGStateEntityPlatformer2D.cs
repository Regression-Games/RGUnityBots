using System;
using System.Collections.Generic;

namespace RegressionGames.RGBotConfigs
{
    [Serializable]
    public class RGStateEntityPlatformer2D
    {
        public float spriteWidth = 1f;
        
        public List<RGStateEntity2DRaycastInfo> leftdown = new();
        public List<RGStateEntity2DRaycastInfo> leftup = new();
        public List<RGStateEntity2DRaycastInfo> rightdown = new();
        public List<RGStateEntity2DRaycastInfo> rightup = new();
        public RGStateEntity2DRaycastInfo down = new();
        public RGStateEntity2DRaycastInfo up = new();
        public List<RGStateEntity2DRaycastInfo> upleft = new();
        public List<RGStateEntity2DRaycastInfo> upright = new();
        public List<RGStateEntity2DRaycastInfo> downleft = new();
        public List<RGStateEntity2DRaycastInfo> downright = new();
        public RGStateEntity2DRaycastInfo left = new();
        public RGStateEntity2DRaycastInfo right = new();
    }
}
