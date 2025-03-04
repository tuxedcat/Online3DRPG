﻿using UnityEngine;
using TeraTaleNet;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

public abstract class Enemy : AliveEntity
{
    [SerializeField]
    float _hpMax = 0;
    [SerializeField]
    float _staminaMax = 0;
    [SerializeField]
    float _baseAttackDamage = 0;
    public AttackSubject _attackSubject;
    public Text nameView;
    protected Animator _animator;

    public class TargetDamagePair : System.IComparable<TargetDamagePair>
    {
        public AliveEntity target;
        public float accumulatedDamage;

        public TargetDamagePair(AliveEntity target, float accumulatedDamage)
        {
            this.target = target;
            this.accumulatedDamage = accumulatedDamage;
        }

        public int CompareTo(TargetDamagePair other)
        {
            if (accumulatedDamage < other.accumulatedDamage)
                return -1;
            if (other.accumulatedDamage < accumulatedDamage)
                return 1;
            return 0;
        }
    }
    List<TargetDamagePair> _targets = new List<TargetDamagePair>();
    public override float hpMax { get { return _hpMax; } }
    public override float staminaMax { get { return _staminaMax; } }
    public override float baseAttackDamage { get { return _baseAttackDamage; } }
    public override float bonusAttackDamage { get { return 0; } }
    public override float baseAttackSpeed { get { return 1; } }
    public override float bonusAttackSpeed { get { return 0; } }
    public override float bonusMoveSpeed { get { return 0; } }
    //return high-damaged target;
    public AliveEntity mainTarget
    { get { return _targets.Count > 0 ? _targets[_targets.Count - 1].target : null; } }
    public List<AliveEntity> targets { get { return (from pair in _targets select pair.target).ToList(); } }
    public MonsterSpawner spawner { get; set; }
    public virtual float respawnDelay { get { return 10; } }
    public virtual float hideTime { get { return 2; } }


    public bool ContainsTarget(GameObject gameObject)
    {
        return _targets.Find(t => t.target.gameObject == gameObject) != null;
    }
    
    public void OnNetInstantiate(ItemSolidArgument arg)
    {
        gameObject.SetActive(false);
    }

    protected void Awake()
    {
        _animator = GetComponentInChildren<Animator>();
    }

    protected new void Start()
    {
        base.Start();
        nameView.text = name;
    }

    public void FacingDirectionUpdate()
    {
        if (mainTarget)
        {
            var vec = mainTarget.transform.position - transform.position;
            transform.LookAt(Vector3.Slerp(transform.forward, vec.normalized, 0.1f) + transform.position);
        }
    }

    void AttackBegin()
    {
        _attackSubject.enabled = true;
    }

    void AttackEnd()
    {
        _attackSubject.enabled = false;
    }

    public virtual void OnAttackAnimationEnd(Collider ardColl)
    {
        ardColl.enabled = true;
    }

    public void AddTarget(AliveEntity target)
    {
        if (isServer)
        {
            var rpc = new AddTarget(target.networkID);
            AddTarget(rpc);
            Send(rpc);
        }
    }

    public void AddTarget(AddTarget rpc)
    {
        if (_targets.Find(t => t.target.networkID == rpc.targetID) == null)
        {
            //target can be null when TargetPlayer is being deleted by some situation like SwitchWorld, Die, etc...
            if (_targets.Count > 0 && _targets.First().target == null)
                _targets.RemoveAt(0);
            _targets.Add(new TargetDamagePair((AliveEntity)NetworkProgramUnity.currentInstance.signallersByID[rpc.targetID], 0));
            _targets.Sort();
        }
    }

    public void RemoveTarget(AliveEntity target)
    {
        if (isServer)
            Send(new RemoveTarget(target.networkID));
    }

    public void RemoveTarget(RemoveTarget rpc)
    {
        var pair = _targets.Find(t => t.target.networkID == rpc.targetID);
        if (pair != null)
            _targets.Remove(pair);
    }

    public void Chase()
    {
        _animator.SetBool("Chase", true);
    }

    public void ChaseStop()
    {
        _animator.SetBool("Chase", false);
    }

    public virtual void Attack()
    {
        _animator.SetTrigger("Attack");
    }

    public bool CanAttackTarget()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position + Vector3.up, transform.forward, out hit, float.MaxValue))
        {
            var root = hit.transform.root;
            if (root.tag == "Player")
            {
                var player = root.GetComponent<Player>();
                if (player == mainTarget)
                    return true;
            }
        }
        return false;
    }

    protected abstract List<Item> itemsForDrop
    { get; }

    protected abstract float levelForDrop
    { get; }

    protected override void Die()
    {
        _animator.SetTrigger("Die");
    }

    public void DropItems()
    {
        if (isLocal)
            return;
        foreach (var item in itemsForDrop)
        {
            float xzAngle;
            do xzAngle = Random.Range(0f, Mathf.PI * 2);
            while (Physics.Raycast(transform.position + Vector3.up, new Vector3(Mathf.Sin(xzAngle), 0, Mathf.Cos(xzAngle)), 2f, LayerMask.GetMask("Terrain")));

            NetworkInstantiate(item.solidPrefab.GetComponent<NetworkScript>(), new ItemSolidArgument(item, transform.position + Vector3.up, xzAngle, Random.Range(0f, 1f)));
        }
    }

    void SetActiveFalse()
    {
        if (isLocal)
            return;
        foreach (var target in _targets)
            target.target.ExpUp(new ExpUp(levelForDrop));//나중에 accumulatedDamage 비율 계산해서 주자.
        InvokeRepeating("Respawn", respawnDelay, float.MaxValue);
        Send(new SetActive(false));
    }

    public void Respawn(Respawn rpc)
    {
        transform.position = new Vector3(Mathf.Sin(rpc.positionSeed), 0, Mathf.Cos(rpc.positionSeed)) * rpc.lengthSeed + spawner.transform.position;
        transform.eulerAngles = new Vector3(0, Random.Range(0f, 360f), 0);
        _animator.Rebind();
        _targets.Clear();
    }

    public void Respawn()
    {
        CancelInvoke("Respawn");
        if (gameObject.activeSelf == false)
            Send(new SetActive(true));
        Send(new Respawn(Random.Range(0f, Mathf.PI * 2), Random.Range(0f, spawner.spawnRange)));
    }

    protected override void OnDamaged(Damage damage)
    {
        var targetPair = _targets.Find(pair => pair.target.name == damage.sendedUser);
        if (targetPair == null)
        {
            targetPair = new TargetDamagePair(Player.FindPlayerByName(damage.sendedUser), 0);
            _targets.Add(targetPair);
        }
        targetPair.accumulatedDamage += CalculateDamage(damage);
        _targets.Sort();
    }

    protected override void Knockdown()
    {
        _animator.SetTrigger("Knockdown");
    }
}