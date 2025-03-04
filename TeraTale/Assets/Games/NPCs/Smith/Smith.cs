﻿using System.Collections.Generic;
using TeraTaleNet;
using UnityEngine;

public class Smith : NPC
{
    protected new void Awake()
    {
        base.Awake();
        itemStacks[0].Push(new Axe());
        itemStacks[1].Push(new Dagger());
        itemStacks[2].Push(new Sword());
        itemStacks[3].Push(new Wand());
        itemStacks[4].Push(new FoxTail());

        itemStacks[6].Push(new Apple());
        itemStacks[7].Push(new HpPotion());
        itemStacks[8].Push(new Bone());
        itemStacks[9].Push(new Log());
        itemStacks[10].Push(new IronOre());
        itemStacks[11].Push(new LapisLazuliOre());

        itemStacks[12].Push(new BowScroll());
        itemStacks[13].Push(new RecallScroll());
    }

    protected override List<Script> scripts
    {
        get
        {
            List<Script> _scripts = new List<Script>();

            Script s;
            Script.Command cmd;

            s.commands = new List<Script.Command>();
            s.comment = "안녕, 무슨일로 찾아왔니?";

            cmd.name = "대화";
            cmd.action = () =>
            {
                s.commands = new List<Script.Command>();
                switch (Random.Range(0, 3))
                {
                    case 0:
                        s.comment = "안녕하새오";
                        break;
                    case 1:
                        s.comment = "그래그래";
                        break;
                    case 2:
                        s.comment = "...할 말 없다고";
                        break;
                }
                cmd.name = "나가기";
                cmd.action = () => { NPCDialog.instance.Close(true); };
                s.commands.Add(cmd);
                _scripts.Add(s);

                NPCDialog.instance.Next();
            };
            s.commands.Add(cmd);

            cmd.name = "거래";
            cmd.action = () =>
            {
                NPCShop.instance.Open(this);
                Inventory.instance.gameObject.SetActive(true);
                NPCDialog.instance.Close(false);
            };
            s.commands.Add(cmd);

            //cmd.name = "조합";
            //cmd.action = () =>
            //{
            //};
            //s.commands.Add(cmd);

            cmd.name = "퀘스트";
            cmd.action = () =>
            {
                s.commands = new List<Script.Command>();
                s.comment = "아직 퀘스트가 없어...";
                cmd.name = "나가기";
                cmd.action = () => { NPCDialog.instance.Close(true); };
                s.commands.Add(cmd);
                _scripts.Add(s);

                NPCDialog.instance.Next();
            };
            s.commands.Add(cmd);

            _scripts.Add(s);

            return _scripts;
        }
    }
}