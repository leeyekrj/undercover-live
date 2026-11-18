using System;
using UnityEngine;

namespace UndercoverLive
{
    public enum GamePhase { Menu, Intro, Playing, Paused, Ended }

    /// <summary>
    /// 게임 전체 상태와 시스템 사이 신호. 여러 담당이 각자 파일을 만들 때 서로를 직접 부르지 않고 여기로 주고받는다.
    /// - 메뉴·멈춤·엔딩 화면(UI 담당)이 Phase를 바꾼다.
    /// - 폰·HUD 담당과 NPC 담당은 Phase를 읽고 이벤트를 구독·발생시킨다.
    /// </summary>
    public static class GameEvents
    {
        public static GamePhase Phase { get; private set; } = GamePhase.Menu;
        /// <summary>플레이어 조작·예배 시간표·의심이 돌아가는 상태인가.</summary>
        public static bool IsPlaying => Phase == GamePhase.Playing;

        /// <summary>R로 다시 할 때 메인 화면을 건너뛰고 바로 도입부터.</summary>
        public static bool SkipMenuOnce;

        public static event Action<GamePhase, GamePhase> PhaseChanged;   // (이전, 지금)

        /// <summary>관리자가 검문을 시작함 (NPC 담당이 발생). seconds 안에 폰을 내리면 통과.</summary>
        public static event Action<float> CheckpointStarted;
        /// <summary>검문 결과. true = 넘어감, false = 폰을 빼앗김.</summary>
        public static event Action<bool> CheckpointEnded;
        /// <summary>폰을 빼앗기는 연출이 끝남 → UI 담당이 발각 엔딩 화면을 띄운다. 인자 = 마지막 한 줄 사유.</summary>
        public static event Action<string> Confiscated;
        /// <summary>누군가 플레이어를 보기 시작함/의심이 크게 오름 (폰 위 경고 배너용). 인자 = 방향·인물 설명 문장.</summary>
        public static event Action<string> Warning;
        /// <summary>플레이어가 일어섬/앉음 (NPC 반응용).</summary>
        public static event Action<bool> PlayerStoodChanged;
        /// <summary>예배가 끝까지 감 → 업로드 결과 화면.</summary>
        public static event Action ServiceFinished;
        /// <summary>플레이어가 일어선 채 무대(교주) 쪽으로 가까이 감 (NPC 담당이 발생, 채팅·교주 반응용).</summary>
        public static event Action PlayerNearStage;
        /// <summary>채팅 상황 신호: 어느 시스템이든 "지금 이런 상황"을 채팅에 알린다. 키는 채팅 문구의 situation 키(phase_sermon, stand_up, checkpoint 등).</summary>
        public static event Action<string> ChatCue;

        /// <summary>
        /// 씬을 다시 불러오기 직전(UIFlow.Restart)에 부른다: Phase = Menu, 이벤트 구독 전부 비움. SkipMenuOnce는 유지.
        /// 옛 씬 구독자가 남아 예외를 던지면 새 씬 구독자가 불리지 않는 문제를 막는다.
        /// </summary>
        public static void ResetForReload() { ResetStatics(); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Phase = GamePhase.Menu;
            PhaseChanged = null; CheckpointStarted = null; CheckpointEnded = null;
            Confiscated = null; Warning = null; PlayerStoodChanged = null; ServiceFinished = null;
            PlayerNearStage = null; ChatCue = null;
            // SkipMenuOnce는 씬을 다시 불러도 유지되어야 해서 지우지 않는다
        }

        public static void SetPhase(GamePhase p)
        {
            if (p == Phase) return;
            var prev = Phase;
            Phase = p;
            PhaseChanged?.Invoke(prev, p);
        }

        public static void RaiseCheckpointStarted(float seconds) => CheckpointStarted?.Invoke(seconds);
        public static void RaiseCheckpointEnded(bool passed) => CheckpointEnded?.Invoke(passed);
        public static void RaiseConfiscated(string reason) => Confiscated?.Invoke(reason);
        public static void RaiseWarning(string text) => Warning?.Invoke(text);
        public static void RaisePlayerStood(bool standing) => PlayerStoodChanged?.Invoke(standing);
        public static void RaiseServiceFinished() => ServiceFinished?.Invoke();
        public static void RaisePlayerNearStage() => PlayerNearStage?.Invoke();
        public static void RaiseChatCue(string situation) => ChatCue?.Invoke(situation);
    }
}
