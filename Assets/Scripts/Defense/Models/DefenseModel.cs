using UnityEngine;

namespace Zpd.Defense
{
    /// <summary>Run-local state and rules. No scene objects, UI, input or network dependencies.</summary>
    public sealed class DefenseModel
    {
        public DefensePhase Phase { get; internal set; }
        public float PhaseRemaining { get; internal set; }
        public DefenseState State { get; internal set; }
        public int Kills { get; internal set; }
        public int Wave { get; internal set; }
        public int PlayerHealth { get; internal set; } = 100;
        public int BeaconHealth { get; internal set; } = 100;
        public string RunId { get; internal set; }
        public float Elapsed { get; internal set; }
        public float FireAt { get; internal set; }
        public float SpawnIn { get; internal set; }
        public float HurtUntil { get; internal set; }
        public float DashUntil { get; internal set; }
        public float NextDash { get; internal set; }
        public int Remaining { get; internal set; }
        public int Spawned { get; internal set; }
        public int VolleyId { get; internal set; }
        public int EntranceCount { get; internal set; }
        public Vector2[] Entrances { get; } = new Vector2[2];
        public float DashReady => Mathf.Clamp01(1 - (NextDash - Elapsed) / 2);

        public void BeginWave(DefenseWaveRules rules, Vector2 arenaHalfSize)
        {
            Phase = DefensePhase.Warning;
            PhaseRemaining = Mathf.Max(0.5f, rules.warning_seconds);
            Remaining = rules.Count(Wave);
            Spawned = 0;
            SpawnIn = 0;
            EntranceCount = Wave >= rules.two_entrances_from_wave ? 2 : 1;
            int edge = (Wave - 1) % 4;

            for (int i = 0; i < EntranceCount; i++)
            {
                int side = i == 0 ? edge : edge ^ 1;
                Entrances[i] = side < 2
                    ? new Vector2(side == 0 ? -arenaHalfSize.x : arenaHalfSize.x, 0)
                    : new Vector2(0, side == 2 ? -arenaHalfSize.y : arenaHalfSize.y);
            }
        }

        public bool CanAdvanceWave(bool cardChosen) => State == DefenseState.Playing && Phase == DefensePhase.Preparation && cardChosen;

        public bool TryAdvanceWave(bool cardChosen)
        {
            if (!CanAdvanceWave(cardChosen))
            {
                return false;
            }

            Wave++;
            return true;
        }

        public void BeginPreparation(DefenseWaveRules rules)
        {
            Phase = DefensePhase.Preparation;
            PhaseRemaining = Mathf.Max(1, rules.preparation_seconds);
        }

        public void Start(string runId)
        {
            RunId = runId;
            State = DefenseState.Playing;
            Wave = 1;
            Kills = 0;
            PlayerHealth = BeaconHealth = 100;
            Elapsed = FireAt = SpawnIn = HurtUntil = DashUntil = NextDash = PhaseRemaining = 0;
            Remaining = Spawned = VolleyId = EntranceCount = 0;
            Phase = DefensePhase.Warning;
        }

        public bool Pause()
        {
            if (State != DefenseState.Playing)
            {
                return false;
            }

            State = DefenseState.Paused;
            return true;
        }

        public bool Resume()
        {
            if (State != DefenseState.Paused)
            {
                return false;
            }

            State = DefenseState.Playing;
            return true;
        }

        public bool TryDash(bool requested)
        {
            if (!requested || State != DefenseState.Playing || Elapsed < NextDash)
            {
                return false;
            }

            DashUntil = Elapsed + 0.16f;
            NextDash = Elapsed + 2;
            HurtUntil = Mathf.Max(HurtUntil, DashUntil);
            return true;
        }

        public int Heal(int amount)
        {
            if (State != DefenseState.Playing)
            {
                return 0;
            }

            int restored = Mathf.Clamp(amount, 0, 100 - PlayerHealth);
            PlayerHealth += restored;
            return restored;
        }

        public int RepairBeacon(int amount)
        {
            if (State != DefenseState.Playing || Phase != DefensePhase.Preparation)
            {
                return 0;
            }

            int restored = Mathf.Clamp(amount, 0, 100 - BeaconHealth);
            BeaconHealth += restored;
            return restored;
        }

        public int ApplyDamage(bool playerTarget, int amount)
        {
            if (State != DefenseState.Playing || amount <= 0 || (playerTarget && Elapsed < HurtUntil))
            {
                return 0;
            }

            int damage = Mathf.Min(playerTarget ? PlayerHealth : BeaconHealth, amount);

            if (playerTarget)
            {
                PlayerHealth -= damage;

                if (damage > 0)
                {
                    HurtUntil = Elapsed + 0.35f;
                }
            }
            else
            {
                BeaconHealth -= damage;
            }

            return damage;
        }
    }
}
