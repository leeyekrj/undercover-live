namespace UndercoverLive
{
    /// <summary>
    /// 찍을 대상 id ↔ 채팅 상황 키(ChatLines.json의 situation). 채팅(담당 B)이 지금 폰에 잡힌 대상으로 상황을 고를 때 부른다.
    /// 의례 쪽에서 직접 보내는 상황 신호(GameEvents.RaiseChatCue): phase_entry/phase_sermon/phase_blindfold/phase_miracle/phase_closing,
    /// eye_vs_phone(강림 시작 직후 한 번), entity_stare(역관측이 시작되는 순간), fake_doubt(거대 겹눈 첫 확보 직후).
    /// </summary>
    public static class PhenomenaCatalog
    {
        public const string CueEyeVsPhone = "eye_vs_phone";
        public const string CueEntityStare = "entity_stare";
        public const string CueFakeDoubt = "fake_doubt";

        /// <summary>대상 id → 채팅 상황 키. 모르는 id나 소리 대상이면 null.</summary>
        public static string ChatSituationFor(string targetId)
        {
            if (string.IsNullOrEmpty(targetId)) return null;
            if (targetId.StartsWith("ceiling_eyes")) return "framing_ceiling_eyes";
            if (targetId.StartsWith("entity")) return "framing_entity";          // entity, entity_stare
            switch (targetId)
            {
                case "portrait_eyes": return "framing_portrait_eyes";
                case "banner": return "framing_banner_eyes";
                case "threads": return "framing_threads";
                case "shadow": return "framing_shadow";
                case "floor_tentacles": return "framing_floor_tentacles";
                case "kneeling_tentacle": return "framing_kneeling_tentacle";
                case "donation_board": return "framing_donation_board";
                case "leader": return "framing_leader";
                case "crowd": return "framing_crowd";
            }
            return null;
        }

        /// <summary>새 키에 문구가 아직 없을 때 쓸 옛 키(있으면). 감시 카메라 문구는 새 설정과 안 맞아 대신 쓰지 않는다.</summary>
        public static string LegacyFallback(string situation)
        {
            switch (situation)
            {
                case "framing_portrait_eyes": return "framing_portrait";
                case "framing_banner_eyes": return "framing_banner";
            }
            return null;
        }

        /// <summary>이번 설정의 대상 id 전부 (천장 눈은 ceiling_eyes_0~3).</summary>
        public static readonly string[] TargetIds =
        {
            "portrait_eyes", "banner", "ceiling_eyes_0", "ceiling_eyes_1", "ceiling_eyes_2", "ceiling_eyes_3",
            "threads", "donation_board", "shadow", "floor_tentacles", "kneeling_tentacle", "entity", "entity_stare",
            "sermon", "sermon_bankbook", "sermon_lockdown", "leader", "crowd",
        };
    }
}
