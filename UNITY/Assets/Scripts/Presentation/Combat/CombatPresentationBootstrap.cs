using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Battle;
using Game.Tank;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Presentation
{
    /// <summary>Builds the battle tableau and plays presentation from the agreed BattleEvents contract.</summary>
    public sealed class CombatPresentationBootstrap : MonoBehaviour
    {
        private static readonly Vector3[] PlayerPositions = { new(-3.2f, 0f, -1.2f), new(-4.6f, 0f, -2.1f), new(-2.1f, 0f, -2.1f) };
        private static readonly Vector3[] EnemyPositions = { new(3.2f, 0f, 1.2f), new(4.6f, 0f, 2.1f), new(2.1f, 0f, 2.1f), new(5.5f, 0f, 0f) };

        private readonly Dictionary<Combatant, CombatantPresentation> _views = new();
        private readonly List<Material> _transientMaterials = new();
        private readonly Queue<(Action play, float hold)> _cues = new();
        private readonly HashSet<CombatantPresentation> _pendingDismounts = new();
        private Transform _stage;
        private Coroutine _cuePlayback;
        private BattleSystem _battle;
        private bool _battleEnded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            var host = new GameObject("CombatPresentationBootstrap");
            DontDestroyOnLoad(host);
            host.AddComponent<CombatPresentationBootstrap>();
        }

        private void OnEnable()
        {
            BattleEvents.Started += OnBattleStarted;
            BattleEvents.ActionStarted += OnActionStarted;
            BattleEvents.Hit += OnHit;
            BattleEvents.Missed += OnMissed;
            BattleEvents.BoardChanged += OnBoardChanged;
            BattleEvents.EscapeAttempted += OnEscapeAttempted;
            BattleEvents.SkillUsed += OnSkillUsed;
            BattleEvents.Defeated += OnDefeated;
            BattleEvents.TankDisabled += OnTankDisabled;
            BattleEvents.Ended += OnEnded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            if (SceneManager.GetActiveScene().name != "Battle")
            {
                _battle = null;
                _battleEnded = false;
            }
            else if (_battle != null && !_battleEnded &&
                     (_battle.State is BattleState.WaitingForCommands or BattleState.Resolving))
            {
                OnBattleStarted(_battle);
            }
        }

        private void OnDisable()
        {
            BattleEvents.Started -= OnBattleStarted;
            BattleEvents.ActionStarted -= OnActionStarted;
            BattleEvents.Hit -= OnHit;
            BattleEvents.Missed -= OnMissed;
            BattleEvents.BoardChanged -= OnBoardChanged;
            BattleEvents.EscapeAttempted -= OnEscapeAttempted;
            BattleEvents.SkillUsed -= OnSkillUsed;
            BattleEvents.Defeated -= OnDefeated;
            BattleEvents.TankDisabled -= OnTankDisabled;
            BattleEvents.Ended -= OnEnded;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            StopCuePlayback();
            _cues.Clear();
            _pendingDismounts.Clear();
            _views.Clear();
            ClearVisualStage();
        }

        private void OnBattleStarted(BattleSystem battle)
        {
            _battle = battle;
            _battleEnded = false;
            StopCuePlayback();
            ClearVisualStage();
            _views.Clear();
            _cues.Clear();
            _pendingDismounts.Clear();
            _stage = new GameObject("BattleVisualStage").transform;
            _stage.SetParent(transform, false);
            var all = battle.players.Select((unit, i) => (unit, i, player: true))
                .Concat(battle.enemies.Select((unit, i) => (unit, i, player: false)));
            foreach (var (unit, index, player) in all)
            {
                var position = player ? PlayerPositions[index % PlayerPositions.Length] : EnemyPositions[index % EnemyPositions.Length];
                var view = CreateUnitView(unit, position, player);
                _views.Add(unit, view);
            }
        }

        private CombatantPresentation CreateUnitView(Combatant unit, Vector3 position, bool player)
        {
            var holder = new GameObject($"View_{(string.IsNullOrEmpty(unit.id) ? "Demo" : unit.id)}_{unit.groupIndex}");
            holder.transform.SetParent(_stage, false);
            holder.transform.position = position;
            holder.transform.rotation = Quaternion.LookRotation(player ? Vector3.forward : Vector3.back);

            if (player && unit.IsTankActive && unit.tank?.chassis != null)
            {
                var chassis = Resources.Load<GameObject>($"Visuals/{unit.tank.chassis.data.partId}");
                if (chassis != null)
                {
                    var tank = Instantiate(chassis, holder.transform, false);
                    tank.name = "Tank";
                    foreach (var weapon in unit.tank.weapons)
                    {
                        if (weapon == null) continue;
                        var weaponPrefab = Resources.Load<GameObject>($"Visuals/{weapon.data.partId}");
                        if (weaponPrefab == null) continue;
                        var mountName = weapon.data.partId == "WPN_SE_Missile" ? "Mount_SE" :
                            weapon.data.partId is "WPN_MG_77" or "WPN_Flamethrower" ? "Mount_Sub" : "Mount_Main";
                        var mount = tank.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == mountName);
                        if (mount != null)
                        {
                            var gun = Instantiate(weaponPrefab, mount, false);
                            gun.name = weapon.data.partId;
                        }
                    }
                    return new CombatantPresentation(this, unit, holder.transform, tank.transform, null, null);
                }
            }

            if (player && !string.IsNullOrEmpty(unit.id) && unit.id.StartsWith("CHR_"))
            {
                var prefab = Resources.Load<GameObject>($"Visuals/{unit.id}");
                if (prefab != null)
                {
                    var actor = Instantiate(prefab, holder.transform, false);
                    actor.name = "Character";
                    var animator = actor.GetComponentInChildren<Animator>();
                    return new CombatantPresentation(this, unit, holder.transform, actor.transform, animator, null);
                }
            }

            var enemy = CreateEnemySilhouette(holder.transform, unit.id);
            return new CombatantPresentation(this, unit, holder.transform, enemy.transform, null, enemy);
        }

        private GameObject CreateEnemySilhouette(Transform root, string id)
        {
            var color = id == "ENM_TurretBug" ? new Color(.34f, .39f, .31f) :
                id != null && id.Contains("Crab") ? new Color(.55f, .27f, .18f) : new Color(.53f, .43f, .28f);
            var enemy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            enemy.name = "EnemySilhouette";
            enemy.transform.SetParent(root, false);
            enemy.transform.localPosition = new Vector3(0, .68f, 0);
            enemy.transform.localScale = id == "ENM_TurretBug" ? new Vector3(1.1f, .72f, 1.35f) : new Vector3(.9f, .58f, 1.15f);
            var renderer = enemy.GetComponent<Renderer>();
            renderer.sharedMaterial = CreateRuntimeMaterial(color, false);
            return enemy;
        }

        private Material CreateRuntimeMaterial(Color color, bool unlit)
        {
            var shader = unlit ? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color")
                : Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null)
            {
                Debug.LogError("[CombatPresentation] No compatible shader for battle effect materials.");
                return null;
            }
            var material = new Material(shader) { color = color };
            _transientMaterials.Add(material);
            return material;
        }

        private void ClearVisualStage()
        {
            if (_stage != null) Destroy(_stage.gameObject);
            _stage = null;
            foreach (var material in _transientMaterials) if (material != null) Destroy(material);
            _transientMaterials.Clear();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "Battle") return;
            _battle = null;
            _battleEnded = false;
            if (_stage == null) return;
            StopCuePlayback();
            ClearVisualStage();
            _views.Clear();
            _cues.Clear();
            _pendingDismounts.Clear();
        }

        private void EnqueueCue(Action play, float hold)
        {
            const int maxQueuedCues = 32;
            while (_cues.Count >= maxQueuedCues) _cues.Dequeue();
            _cues.Enqueue((play, hold));
            if (_cuePlayback == null) _cuePlayback = StartCoroutine(PlayCueQueue());
        }

        private IEnumerator PlayCueQueue()
        {
            while (_cues.Count > 0)
            {
                var cue = _cues.Dequeue();
                cue.play?.Invoke();
                if (cue.hold > 0f) yield return new WaitForSeconds(cue.hold);
            }
            _cuePlayback = null;
        }

        private void StopCuePlayback()
        {
            if (_cuePlayback == null) return;
            StopCoroutine(_cuePlayback);
            _cuePlayback = null;
        }

        private void OnActionStarted(Combatant actor, ActionType type, PartInstance weapon)
        {
            if (!_views.TryGetValue(actor, out var view)) return;
            var hold = type == ActionType.HumanAttack ? .42f : type == ActionType.TankWeapon ? .22f : .08f;
            EnqueueCue(() =>
            {
                if (type == ActionType.HumanAttack) view.Play("Attack");
                if (type == ActionType.TankWeapon)
                {
                    view.Recoil();
                    view.MuzzleFlash(weapon?.data.partId);
                }
                else if (!actor.IsTankActive && actor.side == Side.Enemy && type is ActionType.HumanAttack or ActionType.Skill)
                    view.EnemyLunge();
            }, hold);
        }

        private void OnHit(Combatant attacker, Combatant target, int damage, bool hitTank)
        {
            if (_views.TryGetValue(target, out var view))
                EnqueueCue(() =>
                {
                    if (hitTank) view.TankHit();
                    else view.Play("Hit");
                    view.DamageFlash();
                }, .20f);
        }

        private void OnMissed(Combatant attacker, Combatant target)
        {
            if (_views.TryGetValue(target, out var view)) EnqueueCue(view.Evade, .14f);
        }

        private void OnDefeated(Combatant target)
        {
            if (_views.TryGetValue(target, out var view)) EnqueueCue(() => view.Play("Defeat"), .48f);
        }

        private void OnBoardChanged(Combatant actor, bool inTank)
        {
            if (!_views.TryGetValue(actor, out var previous) || actor.side != Side.Player) return;
            EnqueueCue(() => ReplaceView(actor, previous), .18f);
        }

        private void ReplaceView(Combatant actor, CombatantPresentation previous)
        {
            if (_stage == null || !actor.IsAlive) return;
            var position = previous.Root != null ? previous.Root.localPosition : Vector3.zero;
            Destroy(previous.Root != null ? previous.Root.gameObject : null);
            _views[actor] = CreateUnitView(actor, position, actor.side == Side.Player);
        }

        private void OnEscapeAttempted(Combatant actor, bool escaped)
        {
            if (!escaped && _views.TryGetValue(actor, out var view)) EnqueueCue(view.Evade, .14f);
        }

        private void OnSkillUsed(Combatant actor, string skillId)
        {
            if (_views.TryGetValue(actor, out var view))
                EnqueueCue(() =>
                {
                    if (!actor.IsTankActive) view.Play("Attack");
                }, .24f);
        }

        private void OnTankDisabled(Combatant owner)
        {
            if (owner.side != Side.Player || !_views.TryGetValue(owner, out var view)) return;
            _pendingDismounts.Add(view);
            EnqueueCue(() =>
            {
                ReplaceWithDismountedActor(owner, view);
                _pendingDismounts.Remove(view);
            }, .18f);
        }

        private void ReplaceWithDismountedActor(Combatant owner, CombatantPresentation previous)
        {
            if (!_views.TryGetValue(owner, out var current) || current != previous) return;
            var position = previous.Root != null ? previous.Root.localPosition : Vector3.zero;
            Destroy(previous.Root != null ? previous.Root.gameObject : null);
            _views[owner] = CreateUnitView(owner, position, true);
            _views[owner].Play("Hit");
        }

        private void OnEnded(BattleState state, int exp, int money)
        {
            _battleEnded = true;
            StopCuePlayback();
            _cues.Clear();
            foreach (var view in _pendingDismounts.ToArray())
                ReplaceWithDismountedActor(view.Unit, view);
            _pendingDismounts.Clear();
            foreach (var view in _views.Values.Where(view => !view.Unit.IsAlive)) view.Play("Defeat");
            if (_stage != null) StartCoroutine(ClearAfterDelay(1.55f));
        }

        private void OnDestroy() => ClearVisualStage();

        private IEnumerator ClearAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            ClearVisualStage();
            _views.Clear();
        }

        private sealed class CombatantPresentation
        {
            private readonly CombatPresentationBootstrap _owner;
            private readonly Combatant _unit;
            private readonly Transform _root;
            private readonly Transform _body;
            private readonly Animator _animator;
            private readonly GameObject _enemy;

            public CombatantPresentation(CombatPresentationBootstrap owner, Combatant unit, Transform root, Transform body, Animator animator, GameObject enemy)
            {
                _owner = owner;
                _unit = unit;
                _root = root;
                _body = body;
                _animator = animator;
                _enemy = enemy;
            }

            public Combatant Unit => _unit;
            public Transform Root => _root;

            public void Play(string clip)
            {
                if (_enemy != null && clip == "Defeat")
                {
                    if (_owner != null) _owner.StartCoroutine(EnemyDefeatRoutine());
                    return;
                }
                if (_enemy != null && clip == "Hit")
                {
                    if (_owner != null) _owner.StartCoroutine(EnemyHitRoutine());
                    return;
                }
                if (_animator == null || !_animator.isActiveAndEnabled)
                {
                    if (clip == "Defeat" && _body != null && _owner != null) _owner.StartCoroutine(TankDefeatRoutine());
                    return;
                }
                var trigger = clip == "Attack" ? "Attack" : clip == "Hit" ? "Hit" : "Defeat";
                _animator.ResetTrigger("Attack");
                _animator.ResetTrigger("Hit");
                _animator.SetTrigger(trigger);
            }

            private IEnumerator EnemyDefeatRoutine()
            {
                if (_enemy == null) yield break;
                var startScale = _enemy.transform.localScale;
                var startRotation = _enemy.transform.localRotation;
                for (var i = 1; i <= 10; i++)
                {
                    if (_enemy == null) yield break;
                    var t = i / 10f;
                    _enemy.transform.localScale = Vector3.Lerp(startScale, startScale * .12f, t);
                    _enemy.transform.localRotation = Quaternion.Slerp(startRotation, Quaternion.Euler(0f, 0f, 82f), t);
                    yield return new WaitForSeconds(.035f);
                }
                if (_enemy != null) _enemy.SetActive(false);
            }

            private IEnumerator EnemyHitRoutine()
            {
                if (_enemy == null) yield break;
                var startScale = _enemy.transform.localScale;
                _enemy.transform.localScale = Vector3.Scale(startScale, new Vector3(.78f, 1.16f, .78f));
                yield return new WaitForSeconds(.09f);
                if (_enemy != null) _enemy.transform.localScale = startScale;
            }

            private IEnumerator TankDefeatRoutine()
            {
                if (_body == null) yield break;
                var startPosition = _body.localPosition;
                var startRotation = _body.localRotation;
                _body.localPosition += Vector3.down * .12f;
                _body.localRotation = startRotation * Quaternion.Euler(0f, 0f, -12f);
                yield return new WaitForSeconds(.42f);
                if (_body != null)
                {
                    _body.localPosition = startPosition;
                    _body.localRotation = startRotation;
                }
            }

            public void Recoil() { if (_owner != null) _owner.StartCoroutine(RecoilRoutine()); }

            private IEnumerator RecoilRoutine()
            {
                if (_body == null) yield break;
                var start = _body.localPosition;
                _body.localPosition = start + Vector3.back * .16f;
                yield return new WaitForSeconds(.09f);
                if (_body != null) _body.localPosition = start;
            }

            public void TankHit() => Recoil();
            public void Evade() { if (_owner != null) _owner.StartCoroutine(EvadeRoutine()); }
            public void EnemyLunge() { if (_enemy != null && _owner != null) _owner.StartCoroutine(LungeRoutine()); }

            private IEnumerator LungeRoutine()
            {
                if (_root == null) yield break;
                var start = _root.localPosition;
                _root.localPosition = start + (_unit.side == Side.Enemy ? Vector3.back : Vector3.forward) * .28f;
                yield return new WaitForSeconds(.14f);
                if (_root != null) _root.localPosition = start;
            }

            private IEnumerator EvadeRoutine()
            {
                if (_body == null) yield break;
                var start = _body.localPosition;
                _body.localPosition = start + Vector3.left * .24f;
                yield return new WaitForSeconds(.13f);
                if (_body != null) _body.localPosition = start;
            }

            public void MuzzleFlash(string weaponId)
            {
                if (_body == null) return;
                var muzzle = _body.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(t => t.name == weaponId)?.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(t => t.name.Contains("Muzzle"));
                if (muzzle == null) muzzle = _body;
                var flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                flash.name = "MuzzleFlash";
                flash.transform.position = muzzle.position + muzzle.forward * .45f;
                flash.transform.localScale = Vector3.one * .38f;
                var collider = flash.GetComponent<Collider>();
                if (collider != null) Destroy(collider);
                var mat = _owner != null ? _owner.CreateRuntimeMaterial(new Color(1f, .58f, .16f), true) : null;
                flash.GetComponent<Renderer>().sharedMaterial = mat;
                Object.Destroy(flash, .12f);
            }

            public void DamageFlash()
            {
                if (_enemy == null) return;
                var renderer = _enemy.GetComponent<Renderer>();
                if (renderer == null) return;
                var original = renderer.sharedMaterial.color;
                renderer.sharedMaterial.color = Color.white;
                if (_owner != null) _owner.StartCoroutine(RestoreColor(renderer, original));
            }

            private static IEnumerator RestoreColor(Renderer renderer, Color original)
            {
                yield return new WaitForSeconds(.12f);
                if (renderer != null && renderer.sharedMaterial != null) renderer.sharedMaterial.color = original;
            }

        }
    }
}
