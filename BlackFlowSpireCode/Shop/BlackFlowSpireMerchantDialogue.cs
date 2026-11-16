namespace BlackFlowSpire.Shop;

internal enum BlackFlowSpireMerchantDialogueGroup
{
    Browse, RecruitTicket, Relic, ActiveTool, Scrap,
    BankEntry, BankInvest, BankFaulty, BankWithdrawal, BankRewardUnlock,
    OuterNormal, OuterReward, FightBoss,
    Purchase, SaleMode, SaleSelection, SaleComplete
}

// Original shopDialogData groups. Unimplemented shop systems keep their own
// groups so their dialogue can be connected without mixing unrelated lines.
internal sealed class BlackFlowSpireMerchantDialogue
{
    private readonly Dictionary<BlackFlowSpireMerchantDialogueGroup, Queue<int>> _remaining = [];
    private readonly Dictionary<BlackFlowSpireMerchantDialogueGroup, int> _last = [];

    public string Next(BlackFlowSpireMerchantDialogueGroup group, bool secret = false)
    {
        if (secret)
            return "UI.SECRET_SHOP.dialogue." + (group is BlackFlowSpireMerchantDialogueGroup.SaleMode
                or BlackFlowSpireMerchantDialogueGroup.SaleSelection or BlackFlowSpireMerchantDialogueGroup.SaleComplete ? "sale" : "garden");
        var (key, count) = group switch
        {
            BlackFlowSpireMerchantDialogueGroup.Browse => ("BUY_SELECT.NONE", 11),
            BlackFlowSpireMerchantDialogueGroup.RecruitTicket => ("BUY_SELECT.RECRUIT_TICKET", 11),
            BlackFlowSpireMerchantDialogueGroup.Relic => ("BUY_SELECT.RELIC", 11),
            BlackFlowSpireMerchantDialogueGroup.ActiveTool => ("BUY_SELECT.ACTIVE_TOOL", 3),
            BlackFlowSpireMerchantDialogueGroup.Scrap => ("BUY_SELECT.SCRAP", 3),
            BlackFlowSpireMerchantDialogueGroup.BankEntry => ("BANK_ENTRY.NONE", 3),
            BlackFlowSpireMerchantDialogueGroup.BankInvest => ("BANK_INVEST.NONE", 2),
            BlackFlowSpireMerchantDialogueGroup.BankFaulty => ("BANK_FAULTY.NONE", 4),
            BlackFlowSpireMerchantDialogueGroup.BankWithdrawal => ("BANK_WITHDRAWAL.NONE", 3),
            BlackFlowSpireMerchantDialogueGroup.BankRewardUnlock => ("BANK_REWARD_UNLOCK.NONE", 4),
            BlackFlowSpireMerchantDialogueGroup.OuterNormal => ("OUTER_NORMAL.NONE", 4),
            BlackFlowSpireMerchantDialogueGroup.OuterReward => ("OUTER_REWARD.NONE", 1),
            BlackFlowSpireMerchantDialogueGroup.FightBoss => ("FIGHT_BOSS.NONE", 4),
            BlackFlowSpireMerchantDialogueGroup.Purchase => ("BUY_CONFIRM.NONE", 3),
            BlackFlowSpireMerchantDialogueGroup.SaleMode => ("RECYCLE_CHANGE.NONE", 3),
            BlackFlowSpireMerchantDialogueGroup.SaleSelection => ("RECYCLE_SELECT.NONE", 3),
            BlackFlowSpireMerchantDialogueGroup.SaleComplete => ("RECYCLE_CONFIRM.NONE", 3),
            _ => throw new ArgumentOutOfRangeException(nameof(group), group, null)
        };
        if (!_remaining.TryGetValue(group, out var remaining) || remaining.Count == 0)
        {
            var indices = Enumerable.Range(0, count).ToArray();
            // Cosmetic randomness must not advance the shop/card/part RNG streams.
            Random.Shared.Shuffle(indices);
            if (count > 1 && _last.TryGetValue(group, out var last) && indices[0] == last)
                (indices[0], indices[1]) = (indices[1], indices[0]);
            _remaining[group] = remaining = new Queue<int>(indices);
        }
        var index = remaining.Dequeue();
        _last[group] = index;
        return "UI.SHOP.DIALOGUE." + key + "." + index.ToString("D2", System.Globalization.CultureInfo.InvariantCulture);
    }
}
