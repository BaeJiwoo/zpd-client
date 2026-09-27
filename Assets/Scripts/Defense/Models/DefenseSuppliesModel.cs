namespace Zpd.Defense
{
    /// <summary>Run-local wallet and upgrade policy, independent of UI and network services.</summary>
    public sealed class DefenseSuppliesModel
    {
        public DefenseCombatStats Stats { get; } = new DefenseCombatStats();
        public int Gold { get; private set; }
        public bool ShopOpen { get; private set; }
        public bool RepairedThisWave { get; private set; }
        public bool CardChosen { get; private set; }
        public bool AwaitingCard => ShopOpen && !CardChosen;
        public int SelectedCard { get; private set; } = -1;
        public string[] OfferDescriptions { get; } = new string[3];

        public void Reset()
        {
            Gold = 0;
            Stats.Reset();
            ShopOpen = RepairedThisWave = CardChosen = false;
            SelectedCard = -1;
        }

        public void Collect(int amount)
        {
            if (amount > 0)
            {
                Gold += amount;
            }
        }

        public bool TrySpend(int amount)
        {
            if (amount < 0 || Gold < amount)
            {
                return false;
            }

            Gold -= amount;
            return true;
        }

        public void Open()
        {
            if (ShopOpen)
            {
                return;
            }

            ShopOpen = true;
            RepairedThisWave = false;
            SelectedCard = -1;
            CardChosen = true;

            for (int i = 0; i < 3; i++)
            {
                var command = DefenseUpgradeCatalog.At(i);
                OfferDescriptions[i] = command.Describe(Stats);

                if (command.CanApply(Stats))
                {
                    CardChosen = false;
                }
            }
        }

        public void Close() => ShopOpen = false;

        public void MarkRepaired() => RepairedThisWave = true;

        public bool CanShop(DefenseModel run) => run.State == DefenseState.Playing && run.Phase == DefensePhase.Preparation && ShopOpen && run.PhaseRemaining > 0;

        public bool TryChooseCard(int index, DefenseModel run)
        {
            var command = DefenseUpgradeCatalog.At(index);

            if (!CanShop(run) || CardChosen || command == null || !command.CanApply(Stats))
            {
                return false;
            }

            command.Apply(Stats);
            SelectedCard = index;
            CardChosen = true;
            return true;
        }
    }
}
