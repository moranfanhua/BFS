namespace BlackFlowSpire.Shop;

internal enum ForestMerchantDialogueGroup
{
    Browse, RecruitTicket, Relic, ActiveTool, Scrap,
    BankEntry, BankInvest, BankFaulty, BankWithdrawal, BankRewardUnlock,
    OuterNormal, OuterReward, FightBoss,
    Purchase, SaleMode, SaleSelection, SaleComplete
}

// Original shopDialogData groups. Unimplemented shop systems keep their own
// groups so their dialogue can be connected without mixing unrelated lines.
internal sealed class ForestMerchantDialogue
{
    private readonly Dictionary<ForestMerchantDialogueGroup, Queue<int>> _remaining = [];
    private readonly Dictionary<ForestMerchantDialogueGroup, int> _last = [];

    public string Next(ForestMerchantDialogueGroup group, bool secret = false)
    {
        if (secret)
            return "UI.SECRET_SHOP.dialogue." + (group is ForestMerchantDialogueGroup.SaleMode
                or ForestMerchantDialogueGroup.SaleSelection or ForestMerchantDialogueGroup.SaleComplete ? "sale" : "garden");
        var (key, count) = group switch
        {
            ForestMerchantDialogueGroup.Browse => ("BUY_SELECT.NONE", 11),
            ForestMerchantDialogueGroup.RecruitTicket => ("BUY_SELECT.RECRUIT_TICKET", 11),
            ForestMerchantDialogueGroup.Relic => ("BUY_SELECT.RELIC", 11),
            ForestMerchantDialogueGroup.ActiveTool => ("BUY_SELECT.ACTIVE_TOOL", 3),
            ForestMerchantDialogueGroup.Scrap => ("BUY_SELECT.SCRAP", 3),
            ForestMerchantDialogueGroup.BankEntry => ("BANK_ENTRY.NONE", 3),
            ForestMerchantDialogueGroup.BankInvest => ("BANK_INVEST.NONE", 2),
            ForestMerchantDialogueGroup.BankFaulty => ("BANK_FAULTY.NONE", 4),
            ForestMerchantDialogueGroup.BankWithdrawal => ("BANK_WITHDRAWAL.NONE", 3),
            ForestMerchantDialogueGroup.BankRewardUnlock => ("BANK_REWARD_UNLOCK.NONE", 4),
            ForestMerchantDialogueGroup.OuterNormal => ("OUTER_NORMAL.NONE", 4),
            ForestMerchantDialogueGroup.OuterReward => ("OUTER_REWARD.NONE", 1),
            ForestMerchantDialogueGroup.FightBoss => ("FIGHT_BOSS.NONE", 4),
            ForestMerchantDialogueGroup.Purchase => ("BUY_CONFIRM.NONE", 3),
            ForestMerchantDialogueGroup.SaleMode => ("RECYCLE_CHANGE.NONE", 3),
            ForestMerchantDialogueGroup.SaleSelection => ("RECYCLE_SELECT.NONE", 3),
            ForestMerchantDialogueGroup.SaleComplete => ("RECYCLE_CONFIRM.NONE", 3),
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
