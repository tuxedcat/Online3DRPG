﻿using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TeraTaleNet;
using System.Linq;
using System;

public class Player : AliveEntity
{
    static Dictionary<string, Player> _playersByName = new Dictionary<string, Player>();
    static float[] _hpMaxByLevel = new float[] { 1, 100, 120, 150, 190, 240 };
    static float[] _staminaMaxByLevel = new float[] { 1, 30, 45, 77, 111, 166 };
    static float[] _baseAttackDamageByLevel = new float[] { 1, 12, 13, 15, 18, 22 };
    static float[] _baseAttackSpeedByLevel = new float[] { 1, 1.01f, 1.02f, 1.03f, 1.04f, 1.05f };

    public Text nameView;
    public SpeechBubble speechBubble;
    public ParticleSystem magicCircle;

    public bool gotQuest;

    NavMeshAgent _navMeshAgent;
    Animator _animator;
    public ItemStackList itemStacks = new ItemStackList(30);
    public Weapon _weapon = new WeaponNull();
    ItemSolid _weaponSolid;
    public Accessory _accessory = new AccessoryNull();
    ItemSolid _accessorySolid;
    //Rename Attacker to AttackSubject??
    AttackSubject _attackSubject;
    //StreamingSkill (Base Attack) Management
    static Projectile _pfArrow;
    static Projectile _pfFireBall;
    static Player _pfPlayer;

    public int _money = 0;
    public int money { get { return _money; } private set { _money = value; } }

    public float backTumblingCoolTime { get { return 3f; } }
    public float backTumblingCoolTimeLeft { get; private set; }
    public float skillCoolTime { get { return 5f; } }
    public float skillCoolTimeLeft { get; private set; }

    protected override Color damageTextColor { get { return Color.red; } }
    public override float hpMax { get { return _hpMaxByLevel[level]; } }
    public override float staminaMax { get { return _staminaMaxByLevel[level]; } }
    public override float baseAttackDamage { get { return _baseAttackDamageByLevel[level]; } }
    public override float bonusAttackDamage { get { return _weapon.bonusAttackDamage; } }
    public override float baseAttackSpeed { get { return _baseAttackSpeedByLevel[level]; } }
    public override float bonusAttackSpeed { get { return _weapon.bonusAttackSpeed; } }
    public override float baseMoveSpeed { get { return 3.5f; } }
    public override float bonusMoveSpeed { get { return _accessory.bonusMoveSpeed; } }

    static Player _mine;
    static public Player mine
    {
        get
        {
            if (_mine == null)
                _mine = FindPlayerByName(userName);
            return _mine;
        }
    }

    static public List<Player> players { get { return _playersByName.Values.ToList(); } }

    static public Player FindPlayerByName(string name)
    {
        if (name == null)
            return null;
        Player ret;
        _playersByName.TryGetValue(name, out ret);
        return ret;
    }

    public Weapon weapon
    {
        get { return _weapon; }

        private set
        {
            _weapon = value;
            _animator.SetInteger("WeaponType", (int)_weapon.weaponType);
            if (isServer)
            {
                NetworkDestroy(_weaponSolid);
                NetworkInstantiate(_weapon.solidPrefab.GetComponent<NetworkScript>(), new ItemSolidArgument(_weapon, transform.position + Vector3.up, 0, 0), "OnWeaponInstantiate");
            }
        }
    }

    public Accessory accessory
    {
        get { return _accessory; }

        private set
        {
            _accessory = value;
            if (isServer)
            {
                NetworkDestroy(_accessorySolid);
                NetworkInstantiate(_accessory.solidPrefab.GetComponent<NetworkScript>(), new ItemSolidArgument(_accessory, transform.position + Vector3.up, 0, 0), "OnAccessoryInstantiate");
            }
        }
    }

    public bool isArrived { get { return Vector3.Distance(_navMeshAgent.destination, _navMeshAgent.transform.position) <= _navMeshAgent.stoppingDistance; } }

    void OnWeaponInstantiate(ItemSolid itemSolid)
    {
        _weaponSolid = itemSolid;
        _weapon = (Weapon)_weaponSolid.item;
        _weaponSolid.transform.SetParent(_animator.GetBoneTransform(weapon.targetBone));
        _weaponSolid.transform.localPosition = Vector3.zero;
        _weaponSolid.transform.localRotation = Quaternion.identity;
        _weaponSolid.transform.localScale = new Vector3(0.08f, 0.08f, 0.08f);
        _weaponSolid.enabled = false;
        _weaponSolid.GetComponent<Floater>().enabled = false;
        _weaponSolid.GetComponent<ItemSpawnEffector>().enabled = false;
        //Should Make AttackerNULL and AttackerImpl for ProjectileWeapon
        _attackSubject = _weaponSolid.GetComponent<AttackSubject>();
        _attackSubject.enabled = false;
        _attackSubject.owner = this;
    }

    void OnAccessoryInstantiate(ItemSolid itemSolid)
    {
        _accessorySolid = itemSolid;
        _accessory = (Accessory)_accessorySolid.item;
        _accessorySolid.transform.SetParent(_animator.GetBoneTransform(accessory.targetBone));
        _accessorySolid.transform.localPosition = Vector3.zero;
        _accessorySolid.transform.localRotation = Quaternion.identity;
        _accessorySolid.transform.localScale = new Vector3(0.13f, 0.13f, 0.13f);
        _accessorySolid.enabled = false;
        _accessorySolid.GetComponent<Floater>().enabled = false;
        _accessorySolid.GetComponent<ItemSpawnEffector>().enabled = false;
    }

    protected void Awake()
    {
        for (int i = 0; i < 30; i++)
            itemStacks.Add(new ItemStack());
        _navMeshAgent = GetComponent<NavMeshAgent>();
        _animator = GetComponentInChildren<Animator>();
    }

    protected new void Start()
    {
        base.Start();
        name = owner;
        nameView.text = name;
        try
        {
            _playersByName.Add(name, this);
        }
        catch (ArgumentException e)
        {
            History.Log(e.ToString());
            _playersByName[name] = this;
        }
        if (isMine)
        {
            var cam = FindObjectOfType<CameraController>();
            cam.target = transform;
            Destroy(cam.GetComponent<AudioListener>());
            gameObject.AddComponent<AudioListener>();
            GameObject.FindWithTag("PlayerStatusView").GetComponent<StatusView>().target = this;
        }
        transform.position = GameObject.FindWithTag("SpawnPoint").transform.position;
        if (_pfArrow == null)
            _pfArrow = Resources.Load<Projectile>("Prefabs/Arrow");
        if (_pfFireBall == null)
            _pfFireBall = Resources.Load<Projectile>("Prefabs/FireBall");
        if (_pfPlayer == null)
            _pfPlayer = Resources.Load<Player>("Prefabs/Player");

        if (isServer)
            GameServer.currentInstance.QuerySerializedPlayer(name);
    }

    protected new void Update()
    {
        base.Update();
        backTumblingCoolTimeLeft -= Time.deltaTime;
        skillCoolTimeLeft -= Time.deltaTime;
        stamina += 1 / 60f;
        if (stamina > staminaMax)
            stamina = staminaMax;
    }

    protected override void OnNetworkDestroy()
    {
        if (isServer)
            GameServer.currentInstance.SavePlayer(this);
        base.OnNetworkDestroy();
        _playersByName.Remove(name);
    }

    void AttackBegin()
    {
        _attackSubject.enabled = true;
        _attackSubject.damageCalculator = t => t;
    }

    void AttackEnd()
    {
        _attackSubject.enabled = false;
        _attackSubject.damageCalculator = t => t;
    }

    void SwordSkillBegin()
    {
        _attackSubject.enabled = true;
        _attackSubject.damageCalculator = t => t * 3;
    }

    void SwordSkillEnd()
    {
        _attackSubject.enabled = false;
        _attackSubject.damageCalculator = t => t;
    }

    public void SetKnockdown(bool value)
    {
        _attackSubject.knockdown = value;
    }

    void Shot()
    {
        var projectile = Instantiate(_pfArrow);
        projectile.transform.position = transform.position + Vector3.up;
        projectile.direction = transform.forward;
        projectile.speed = 10;
        projectile.autoDestroyTime = 0.5f;
        projectile.GetComponent<AttackSubject>().owner = this;
    }

    void ShotSkill()
    {
        for (int i = 0; i < 3; i++)
        {
            var projectile = Instantiate(_pfArrow);
            projectile.transform.position = transform.position + Vector3.up;
            projectile.direction = Quaternion.Euler(0, (i - 1) * 5, 0) * transform.forward;
            projectile.speed = 10;
            projectile.autoDestroyTime = 0.8f;
            var attackSubject = projectile.GetComponent<AttackSubject>();
            attackSubject.owner = this;
            if (i == 1)
                attackSubject.knockdown = true;
        }
    }

    void WandAttack()
    {
        var projectile = Instantiate(_pfFireBall);
        projectile.transform.position = transform.position + Vector3.up;
        projectile.direction = transform.forward;
        projectile.speed = 5;
        projectile.autoDestroyTime = 0.8f;
        projectile.GetComponent<AttackSubject>().owner = this;
    }

    void WandSkill()
    {
        for (int i = 0; i < 8; i++)
        {
            var projectile = Instantiate(_pfFireBall);
            projectile.transform.position = transform.position + Vector3.up;
            projectile.direction = Quaternion.Euler(0, 45 * i, 0) * transform.forward;
            projectile.speed = 8;
            projectile.autoDestroyTime = 0.8f;
            var attackSubject = projectile.GetComponent<AttackSubject>();
            attackSubject.owner = this;
            attackSubject.knockdown = true;
        }

        var circle = Instantiate(magicCircle);
        circle.transform.position = transform.position + Vector3.up * 0.1f;
        Destroy(circle.gameObject, 1);
    }

    public void FacingDirectionUpdate()
    {
        var corners = _navMeshAgent.path.corners;
        if (corners.Length >= 2)
        {
            var vec = corners[1] - corners[0];
            transform.LookAt(Vector3.Slerp(transform.forward, vec.normalized, 0.3f) + transform.position);
        }
    }

    public void Attack(Attack info)
    {
        _animator.SetTrigger("Attack");
    }

    public void StopAttack()
    {
        _attackSubject.enabled = false;
    }

    public void Skill(Skill info)
    {
        _animator.SetTrigger("Skill");
        skillCoolTimeLeft = skillCoolTime;
        stamina -= 10;
    }

    public void Skill()
    {
        if (skillCoolTimeLeft < 0f && stamina > 10)
            Send(new Skill());
    }

    public void BackTumbling(BackTumbling info)
    {
        _animator.SetTrigger("BackTumbling");
        backTumblingCoolTimeLeft = backTumblingCoolTime;
        stamina -= 5;
    }

    public void BackTumbling()
    {
        if (backTumblingCoolTimeLeft < 0f & stamina > 5)
            Send(new BackTumbling());
    }

    protected override void Die()
    {
        _animator.SetTrigger("Die");
        if (isMine)
        {
            GlobalSound.instance.PlayDie();
            PlayerDieDialog.instance.Invoke("Show", 3);
        }
        GetComponent<CapsuleCollider>().center = new Vector3(float.MaxValue / 2, float.MaxValue / 2, float.MaxValue / 2);
    }

    public void Respawn(Respawn rpc)
    {
        hp = hpMax;
        SwitchWorld("Town");
    }

    public void Respawn()
    {
        Send(new Respawn());
    }

    protected override void Knockdown()
    {
        _animator.SetTrigger("Knockdown");
    }

    void Navigate(Navigate info)
    {
        _navMeshAgent.destination = info.destination;
        _animator.SetBool("Run", true);
    }

    public void Speak(string chat)
    {
        speechBubble.Show(chat);
    }

    public void SwitchWorld(string world)
    {
        Send(new SwitchWorld(RPCType.Specific, Application.loadedLevelName, name, world));
    }

    public void SwitchWorld(SwitchWorld rpc)
    {
        if (isServer)
        {
            rpc.receiver = rpc.user;
            Send(rpc);
            Destroy();
        }
        else
        {
            SceneManager.LoadScene(rpc.world);
            Send(new BufferedRPCRequest(userName));
            NetworkProgramUnity.currentInstance.NetworkInstantiate(_pfPlayer);
        }
    }

    public void AddItem(Item item)
    {
        Send(new AddItem(item));
    }

    public void AddItem(AddItem rpc)
    {
        Item item = (Item)rpc.item;
        itemStacks.Find((ItemStack s) => { return s.IsPushable(item); }).Push(item);
        if (isMine)
            GlobalSound.instance.PlayItemPick();
    }

    public bool CanAddItem(Item item, int amount)
    {
        return itemStacks.Find((ItemStack s) => { return s.IsPushable(item, amount); }) != null;
    }

    public void ItemUse(ItemUse rpc)
    {
        itemStacks[rpc.index].Use(this);
    }

    public void Use(int itemStackIndex)
    {
        Send(new ItemUse(itemStackIndex));
    }

    public void Equip(Equipment equipment)
    {
        switch (equipment.equipmentType)
        {
            case Equipment.Type.Accessory:
                accessory = (Accessory)equipment;
                break;
            case Equipment.Type.Coat:
                break;
            case Equipment.Type.Gloves:
                break;
            case Equipment.Type.Hat:
                break;
            case Equipment.Type.Pants:
                break;
            case Equipment.Type.Shoes:
                break;
            case Equipment.Type.Weapon:
                weapon = (Weapon)equipment;
                break;
        }
    }

    public bool IsEquiping(Equipment equipment)
    {
        switch (equipment.equipmentType)
        {
            case Equipment.Type.Accessory:
                return accessory == equipment;
            case Equipment.Type.Coat:
                break;
            case Equipment.Type.Gloves:
                break;
            case Equipment.Type.Hat:
                break;
            case Equipment.Type.Pants:
                break;
            case Equipment.Type.Shoes:
                break;
            case Equipment.Type.Weapon:
                return weapon == equipment;
        }
        return false;
    }

    public void AddQuest(AddQuest quest)
    {
        gotQuest = true;
    }

    public void AddQuest()
    {
        Send(new AddQuest());
    }

    public void RemoveQuest(RemoveQuest quest)
    {
        gotQuest = false;
    }

    public void RemoveQuest()
    {
        Send(new RemoveQuest());
    }

    public void SerializedPlayer(SerializedPlayer rpc)
    {
        if (isLocal)
            return;
        if (rpc.data.Length <= 1)
            return;
        Deserialize(rpc.data);

        

        //Deserialize only set the field _weapon, not the property weapon. so, we should call this property manually.
        weapon = weapon;
        //Weapon Sync;
        Sync s = new Sync(RPCType.Others | RPCType.Buffered, "", "weapon");
        s.signallerID = networkID;
        s.sender = userName;
        Sync(s);

        //same as weapon
        accessory = accessory;
        s = new Sync(RPCType.Others | RPCType.Buffered, "", "accessory");
        s.signallerID = networkID;
        s.sender = userName;
        Sync(s);

        //itemStack Sync
        s = new Sync(RPCType.Others | RPCType.Buffered, "", "itemStacks");
        s.signallerID = networkID;
        s.sender = userName;
        Sync(s);

        //itemStack Sync
        s = new Sync(RPCType.Others | RPCType.Buffered, "", "gotQuest");
        s.signallerID = networkID;
        s.sender = userName;
        Sync(s);

        //money Sync
        s = new Sync(RPCType.Others | RPCType.Buffered, "", "money");
        s.signallerID = networkID;
        s.sender = userName;
        Sync(s);
    }

    public void DropItemStack(DropItemStack rpc)
    {
        var itemStack = itemStacks[rpc.index];
        var itemCount = itemStack.count;
        for (int i = 0; i < itemCount; i++)
        {
            var item = itemStack.Pop();
            if (isServer)
                NetworkInstantiate(item.solidPrefab.GetComponent<ItemSolid>(), new ItemSolidArgument(item, transform.position + Vector3.up, 0, 0));
        }
    }

    public void DropItemStack(int index)
    {
        Send(new DropItemStack(index));
    }

    public void SwapItemStack(SwapItemStack rpc)
    {
        var tmp = itemStacks[rpc.indexA];
        itemStacks[rpc.indexA] = itemStacks[rpc.indexB];
        itemStacks[rpc.indexB] = tmp;
    }

    public void SwapItemStack(int indexA, int indexB)
    {
        Send(new SwapItemStack(indexA, indexB));
    }

    public void SellItem(SellItem rpc)
    {
        for (int i = 0; i < rpc.amount; i++)
            money += itemStacks[rpc.index].Pop().price;
    }

    public void SellItem(int index, int amount)
    {
        Send(new SellItem(index, amount));
    }

    public void BuyItem(BuyItem rpc)
    {
        money -= rpc.item.price * rpc.amount;
        for (int i = 0; i < rpc.amount; i++)
            AddItem(rpc.item);

        var s = new Sync(RPCType.Others, "", "money");
        s.signallerID = networkID;
        s.sender = userName;
        Sync(s);
    }

    public void BuyItem(Item item, int amount)
    {
        Send(new BuyItem(Application.loadedLevelName, item, amount));
    }

    public int ItemCount(Item item)
    {
        int count = 0;

        for (int i = 0; i < itemStacks.count; i++)
            if (itemStacks[i].item.IsSameType(item))
                count += itemStacks[i].count;

        return count;
    }

    public void RemoveItem(RemoveItem rpc)
    {
        for (int i = 0; i < itemStacks.count; i++)
        {
            var itemStack = itemStacks[i];
            if (itemStack.item.IsSameType(rpc.item))
            {
                while (itemStack.count != 0)
                {
                    if (rpc.amount == 0)
                        return;
                    itemStack.Pop();
                    --rpc.amount;
                }
            }
        }
    }

    public void RemoveItem(Item item, int amount)
    {
        Send(new RemoveItem(item, amount));
    }
}