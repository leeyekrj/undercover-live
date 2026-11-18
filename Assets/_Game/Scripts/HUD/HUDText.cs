using UnityEngine;

namespace UndercoverLive
{
    /// <summary>
    /// HUD에 뜨는 문구를 한 곳에 모은다 (담당 A의 UIStrings가 생기면 그쪽으로 옮길 자리).
    /// 🚧 설정 변경 중: 기적·커튼 뒤 대상은 '카메라에만 찍히는 눈동자 존재'로 바뀌는 중이라
    /// 속임수 폭로(와이어·조수 등) 문구를 쓰지 않고 중립 자리 문구만 둔다.
    /// </summary>
    public static class HUDText
    {
        // ---------- 대상 이름 (초점 상자 위 한 줄) ----------

        /// <summary>속임수 폭로 대상인가 (이 대상의 기존 폭로 문구는 쓰지 않는다).</summary>
        public static bool IsRevealTarget(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            return id.StartsWith("miracle_wire") || id.StartsWith("assistant");
        }

        public static string TargetLabel(string id, string fallback)
        {
            if (string.IsNullOrEmpty(id)) return fallback;
            if (id.StartsWith("miracle_wire")) return "공중부양 장면";     // 🚧 자리 문구
            if (id.StartsWith("assistant")) return "커튼 뒤";              // 🚧 자리 문구
            if (id.StartsWith("cctv")) return "천장 감시 카메라";
            if (id.StartsWith("banner")) return "현수막";
            if (id.StartsWith("leader")) return "눈동자님";
            if (id.StartsWith("crowd")) return "눈꺼풀들";
            if (id.StartsWith("sermon")) return "설교 녹음";
            if (id.StartsWith("portrait")) return "초상화";
            if (id.StartsWith("countdown")) return "소등 날짜판";
            if (id.StartsWith("donation_board")) return "달성판";
            return string.IsNullOrEmpty(fallback) ? id : fallback;
        }

        // ---------- 채팅 거르기 ----------

        /// <summary>설정 변경 중이라 채팅에 내지 않는 낱말 (속임수 폭로).</summary>
        static readonly string[] Banned = { "와이어", "조수", "눈속임", "뭐 달렸" };

        public static bool IsBanned(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            for (int i = 0; i < Banned.Length; i++)
                if (text.Contains(Banned[i])) return true;
            return false;
        }

        // ---------- 경고 (방향·인물) ----------

        /// <summary>머리 기준 지역 좌표 → 앞·뒤·왼쪽·오른쪽.</summary>
        public static string Where(Vector3 local)
        {
            float a = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            if (Mathf.Abs(a) <= 45f) return "앞";
            if (Mathf.Abs(a) >= 135f) return "뒤";
            return a < 0f ? "왼쪽" : "오른쪽";
        }

        /// <summary>알림 배너(폰 앱 말투)에서 부르는 이름.</summary>
        public static string WhoPlain(WatcherKind k)
        {
            switch (k)
            {
                case WatcherKind.Staff: return "관리자";
                case WatcherKind.Leader: return "교주";
                case WatcherKind.CCTV: return "천장의 붉은 눈";
                default: return "신도";
            }
        }

        /// <summary>채팅(시청자 말투)에서 부르는 이름.</summary>
        public static string WhoChat(WatcherKind k)
        {
            switch (k)
            {
                case WatcherKind.Staff: return "관리자";
                case WatcherKind.Leader: return "눈동자님";
                case WatcherKind.CCTV: return "천장의 붉은 눈";
                default: return "눈꺼풀";
            }
        }

        /// <summary>폰 위 알림 배너 한 줄. 예: "오른쪽에서 관리자가 보고 있어요".</summary>
        public static string BannerLine(string where, WatcherKind k)
        {
            if (k == WatcherKind.CCTV) return "천장의 붉은 눈이 이쪽을 보고 있어요";
            string who = WhoPlain(k);
            return where + "에서 " + who + UIKit.Josa(who) + " 보고 있어요";
        }

        /// <summary>경고 배너의 행동 칩. 공통 규칙(Paul 11-18): 숨기기(넣기)는 어느 상태에서든 [4] 숨기기로 표시.</summary>
        public const string KeyHide = "4", BannerAction = "숨기기";

        // ---------- 상태 ----------

        public const string RecVoiceOnly = "음성만";
        public const string RecSneak = "몰래";
        public const string LiveOff = "끊김";
        public const string BatteryDead = "배터리가 없어요";
        public const string GoalLabel = "오늘 찍을 것";
        public const string AlertLabel = "경계";
        public const string ZoomCaption = "휠 · -/+";

        // ---------- 키 안내 ----------

        public const string KeyRight = "우클릭", KeyZoom = "-/+", KeyE = "E", KeyF = "F", KeyWASD = "WASD";
        public const string ActRaise = "폰 들기", ActLower = "내리기", ActHide = "숨기기", ActZoom = "줌", ActSneak = "몰래 찍기",
            ActSneakOff = "몰래 끄기", ActStand = "일어서기", ActSit = "앉기", ActWalk = "걷기";
    }
}
