﻿using System.Collections.Generic;
using TeraTaleNet;
using UnityEngine;

public class Mineral : Enemy
{
    //protected override void PeriodicSync()
    //{ }

    public override float baseMoveSpeed { get { return 0; } }

    protected override List<Item> itemsForDrop
    {
        get
        {
            List<Item> ret = new List<Item>();
            if (Random.Range(0, 2) == 0)
                ret.Add(new IronOre());
            ret.Add(new IronOre());
            ret.Add(new IronOre());
            ret.Add(new IronOre());
            ret.Add(new IronOre());
            ret.Add(new Rock());
            ret.Add(new Rock());
            ret.Add(new Rock());
            return ret;
        }
    }

    protected override void OnDamaged(Damage damage)
    {
        base.OnDamaged(damage);
        GlobalSound.instance.PlayPickaxe();
    }

    protected override float levelForDrop
    { get { return 10; } }

    protected override float CalculateDamage(Damage damage)
    {
        if (damage.weaponType != Weapon.Type.pickaxe)
        {
            if (damage.sendedUser == userName)
                ChattingView.instance.PushGuideMessage("광물은 '곡괭이'로 채광할 수 있습니다.");
            return 0;
        }
        return damage.amount;
    }
}