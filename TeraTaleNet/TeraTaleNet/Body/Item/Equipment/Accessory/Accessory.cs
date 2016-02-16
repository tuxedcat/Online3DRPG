﻿namespace TeraTaleNet
{
    public abstract class Accessory : Equipment
    {
        public sealed override Type equipmentType { get { return Type.Accessory; } }
        public abstract float bonusMoveSpeed { get; }

        public Accessory()
        { }
    }
}