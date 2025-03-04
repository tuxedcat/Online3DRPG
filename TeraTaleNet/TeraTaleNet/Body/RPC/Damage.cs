﻿namespace TeraTaleNet
{
    public class Damage : RPC
    {
        public enum Type
        {
            Physical,
            Magic,
        }
        public Type damageType;
        public Weapon.Type weaponType;
        public string sendedUser;
        public float amount;
        public float knockback;
        public bool knockdown;

        public Damage(Type damageType, Weapon.Type weaponType, string sendedUser, float amount, float knockback, bool knockdown)
            : base(RPCType.Others)
        {
            this.damageType = damageType;
            this.weaponType = weaponType;
            this.sendedUser = sendedUser;
            this.amount = amount;
            this.knockback = knockback;
            this.knockdown = knockdown;
        }

        public Damage()
            : base(RPCType.Others)
        { }
    }
}