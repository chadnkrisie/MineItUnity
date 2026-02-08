namespace MineIt.Input
{
    public struct InputSnapshot
    {
        public bool MoveUp { get; set; }
        public bool MoveDown { get; set; }
        public bool MoveLeft { get; set; }
        public bool MoveRight { get; set; }

        // One-frame actions
        public bool ScanPressed { get; set; }
        public bool ClaimPressed { get; set; }
        public bool ExtractPressed { get; set; }  // NEW: one-frame extraction action (E)
        public bool DepositPressed { get; set; }  // NEW: deposit backpack into town storage (T)
        public bool SellPressed { get; set; }   // NEW: sell ore for credits (Y)

        // Dev hotkeys (one-frame commands). 0 means "no command this frame".
        public int DevSetDetectorTier { get; set; }   // 1..5
        public int DevSetExtractorTier { get; set; }  // 1..5
        public int DevSetBackpackTier { get; set; }   // 1..5


    }
}
