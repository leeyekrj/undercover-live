namespace UndercoverLive
{
    /// <summary>
    /// 대상별 채팅·업로드 댓글. 종류(분류)만 보고 고르면 엉뚱한 반응이 붙어서,
    /// 대상 id로 먼저 찾고 없을 때만 분류 공용 문구로 돌아간다. id는 앞부분만 맞으면 된다(ceiling_eyes_0 → ceiling_eyes).
    /// 앞부분이 겹치는 id(entity_stare / entity, sermon_bankbook / sermon)는 긴 쪽을 먼저 둔다.
    /// 설정: 카메라에만 찍히는 거대 '겹눈' 존재와 그 흔적들 + 교단의 현실 착취 증거.
    /// </summary>
    public static class TargetLines
    {
        struct Entry
        {
            public string idPrefix;
            public string[] live;        // 확보 순간 라이브 채팅
            public string[][] comments;  // 업로드 결과 카드 댓글 (두 줄 한 묶음)
        }

        static readonly Entry[] Entries =
        {
            new Entry {
                idPrefix = "entity_stare",
                live = new[] { "화면 전체가 눈이야!!!", "형 내려 내려 내려", "렌즈 쪽으로 내려온다 미쳤다", "동공에 형 비치는 거 봄?", "이거 CG 아니지??" },
                comments = new[] {
                    new[] { "렌즈 앞까지 내려온 동공 실화냐", "끝까지 안 내린 형이 제일 무서움" },
                    new[] { "화면 꽉 찬 눈알 보고 소리 지름", "저게 우릴 보고 있었던 거임?" },
                },
            },
            new Entry {
                idPrefix = "entity",
                live = new[] { "천장에 저거 뭐야??", "눈알이 천장을 다 덮었어", "촉수가 단상까지 내려와 있음", "다들 하얀 천 써서 아무도 모름 ㄷㄷ", "형 도망쳐 진짜로" },
                comments = new[] {
                    new[] { "천장 덮은 거대한 눈 실화냐", "하얀 천 쓴 사람들 머리 위에 떠 있었음" },
                    new[] { "맨눈엔 금빛 기둥이라던데 폰엔 저게 찍힘", "역대급 공포 영상 갱신" },
                },
            },
            new Entry {
                idPrefix = "kneeling_tentacle",
                live = new[] { "무릎 꿇은 사람 머리에 뭐가 감겨 있어", "천장에서 촉수 내려왔다", "빛 같은 게 빨려 올라가", "교주 손 아니고 촉수였네" },
                comments = new[] {
                    new[] { "무릎 꿇은 사람 머리 감싼 촉수 보임?", "빛 빨려 올라가는 거 소름" },
                    new[] { "손 얹는 줄 알았는데 촉수였음", "저 사람 괜찮은 거 맞냐" },
                },
            },
            new Entry {
                idPrefix = "floor_tentacles",
                live = new[] { "바닥에 뭐 기어 온다", "형 의자 밑까지 왔어!!", "카운트다운 숫자 미쳐 돌아감", "41일이 00이 됐어" },
                comments = new[] {
                    new[] { "통로 바닥 기어 오는 그림자 봄?", "비추니까 의자 밑으로 숨음 ㄷㄷ" },
                    new[] { "소등 숫자가 00까지 떨어짐", "폰으로만 보이는 게 더 무서움" },
                },
            },
            new Entry {
                idPrefix = "shadow",
                live = new[] { "교주 뒤 그림자 사람 모양 아님", "그림자에 눈이 일곱 개야", "등 뒤 눈들이 렌즈 봤어", "조명이랑 그림자 방향이 다름" },
                comments = new[] {
                    new[] { "교주 등 뒤 그림자에 노란 눈 일곱 개", "그림자가 렌즈 쪽으로 초점 맞춤 ㄷㄷ" },
                    new[] { "사람 그림자가 아니라 촉수 덩어리였음", "이거 보고 잠 다 잤다" },
                },
            },
            new Entry {
                idPrefix = "threads",
                live = new[] { "머리마다 줄 같은 게 꽂혀 있어", "다 같이 고개 꺾는 거 봐", "꼭두각시 줄이네 저거", "앞줄 사람 고개 돌아갔다" },
                comments = new[] {
                    new[] { "정수리마다 꽂힌 줄 보임?", "고개 꺾일 때 줄이 팽팽해짐" },
                    new[] { "사람들이 인형처럼 움직임", "한 명 고개 돌아가는 거 보고 소리 지름" },
                },
            },
            new Entry {
                idPrefix = "ceiling_eyes",
                live = new[] { "천장 구석에 빨간 눈 있음", "구석마다 눈알 덩어리가", "눈알이 이쪽 봤다!!", "빨간 불 세졌어 내려" },
                comments = new[] {
                    new[] { "천장 구석 붉은 눈 덩어리 봄?", "구석마다 하나씩 붙어 있었음" },
                    new[] { "형 쪽으로 휙 돌아보는 거 소름", "맨눈엔 그냥 어두운 천장이래" },
                },
            },
            new Entry {
                idPrefix = "portrait_eyes",
                live = new[] { "초상화 눈알 튀어나왔어", "그림 눈이 렌즈 따라와", "뒤에 금빛 고리 움직임", "동공 세로로 찢어짐 ㄷㄷ" },
                comments = new[] {
                    new[] { "초상화 눈이 렌즈를 따라옴", "금빛 고리 두 개가 눈 모양이 됨" },
                    new[] { "그림 속 동공이 세로로 찢어짐", "첫 장면부터 이상했음" },
                },
            },
            new Entry {
                idPrefix = "donation_board",
                live = new[] { "달성판 뒤에 촉수 세 가닥", "빛 알갱이가 천장으로 빨려 가", "미납자 명단 다 찍혔다", "92%? 얼마나 뜯은 거야" },
                comments = new[] {
                    new[] { "달성판 움켜쥔 촉수 실화냐", "미납자 명단까지 다 찍힘" },
                    new[] { "바깥 짐 비움이 재산 털기였네", "숫자 오를 때 빛이 빨려 올라감" },
                },
            },
            new Entry {
                idPrefix = "banner",
                live = new[] { "현수막 글씨 바뀜", "'모든 돈을 바친다' ㅋㅋ", "글자 사이에 눈알 박혀 있어", "눈알들 같이 깜빡여 ㄷㄷ" },
                comments = new[] {
                    new[] { "현수막 속뜻 '모든 돈을 바친다' 실화냐", "글자 사이 눈알들 깜빡임" },
                    new[] { "맨눈엔 평안이라더니 폰엔 돈 얘기", "이거 캡처해서 박제해야 함" },
                },
            },
            new Entry {
                idPrefix = "leader",
                live = new[] { "눈동자님 ㅋㅋ 호칭부터 소름", "말투 너무 차분해서 무서움", "교주 얼굴 나왔다", "눈빛이 사람 홀리는 관상" },
                comments = new[] {
                    new[] { "눈동자님 ㅋㅋ 호칭부터 소름", "말투가 차분해서 더 무서움" },
                    new[] { "교주 얼굴 정면으로 잡힘", "초상화까지 걸어 놨네" },
                },
            },
            new Entry {
                idPrefix = "crowd",
                live = new[] { "다들 미동도 없어;;", "앞사람들 하나도 안 움직여", "다 같이 같은 말 하는 거 소름", "보는 내가 숨막힌다" },
                comments = new[] {
                    new[] { "다들 홀린 듯 앉아 있는 거 무섭다", "앞사람들 하나도 안 움직여;;" },
                    new[] { "다 같이 같은 말 외우는 거 소름", "보는 내가 다 숨막혀" },
                },
            },
            new Entry {
                idPrefix = "sermon_bankbook",
                live = new[] { "통장 숫자 보지 말래 ㄷㄷ", "돈 다 내놓으란 소리네", "녹음됐다 이거 증거", "대놓고 협박이잖아" },
                comments = new[] {
                    new[] { "'통장 숫자' 협박 녹음 확보", "돈 다 내놓으란 소리를 대놓고 함" },
                    new[] { "바깥 눈은 곧 먼대 ㄷㄷ", "목소리만 들어도 오싹하다" },
                },
            },
            new Entry {
                idPrefix = "sermon_lockdown",
                live = new[] { "극장 문 나가지 말래", "이거 감금 선언 아님?", "녹음 꼭 살려 둬", "제가 지켜봅니다 ㄷㄷ" },
                comments = new[] {
                    new[] { "'극장 문을 나가지 마세요' 감금 선언 녹음", "이건 경찰 가야 됨" },
                    new[] { "제가 지켜봅니다 소름", "녹음 잘 됐다 이거 증거 된다" },
                },
            },
            new Entry {
                idPrefix = "sermon",
                live = new[] { "'차가운 유리 눈' ㅋㅋ 폰 말하는 거임?", "폰 금지가 교리였네", "녹음 됐지?? 다시 들어 봐야 함", "저 말 대놓고 하네" },
                comments = new[] {
                    new[] { "'차가운 유리 눈' 이러면서 폰 걷는 거임?", "녹음 잘 됐다 이거 증거 된다" },
                    new[] { "폰 금지가 교리라니 이유가 있었네", "목소리만 들어도 오싹하다" },
                },
            },
        };

        // 등급 C: 영상과 소리의 실패 이유가 다르다
        static readonly string[][] BadVideo = {
            new[] { "화면 흔들려서 뭔지 모르겠음", "줌 좀 땡겨주세요ㅠ" },
            new[] { "방금 뭐 찍은 거임?", "초점 나간 듯 ㅋㅋ" },
        };
        static readonly string[][] BadSound = {
            new[] { "소리가 너무 작아요 하나도 안 들림", "마이크 좀 가까이 대 봐요" },
            new[] { "웅웅거려서 무슨 말인지 모르겠음", "중간에 끊겼어요ㅠ" },
        };

        // 대상 표에 없을 때 쓰는 분류 공용 댓글
        static readonly string[][][] CategoryComments = {
            new[] { new[] { "분위기 너무 소름... 여기 어디임", "다들 왜 저렇게 가만히 있어;;" }, new[] { "보는 내가 다 불안하다", "이거 진짜 현장 맞아요?" } },
            new[] { new[] { "저건 증거 아님? ㄷㄷ", "캡처 떴다 이거 퍼가야 돼" }, new[] { "글씨까지 다 읽힌다", "저건 신고감 아니냐" } },
            new[] { new[] { "클립 땄다 이거 뉴스 나가야 함", "찍어줘서 고마워요 진짜" }, new[] { "이건 진짜 아니다", "형 제발 안전하게 나와" } },
            new[] { new[] { "방금 저 말 녹음된 거 맞죠", "이거 증거로 쓸 수 있는 거 아님?" }, new[] { "저런 말을 대놓고 하네", "소리 키우니까 더 소름;;" } },
        };

        static bool TryFind(string id, out Entry e)
        {
            if (!string.IsNullOrEmpty(id))
                foreach (var x in Entries)
                    if (id.StartsWith(x.idPrefix)) { e = x; return true; }
            e = default;
            return false;
        }

        /// <summary>확보 순간 라이브 채팅 후보. 없으면 null.</summary>
        public static string[] Live(CaptureResult r)
        {
            return TryFind(r.TargetId, out var e) ? e.live : null;
        }

        /// <summary>업로드 결과 카드의 댓글 두 줄.</summary>
        public static string[] Comments(CaptureResult r, int index)
        {
            if (r.Grade == CaptureGrade.C)
            {
                var bad = r.Category == CaptureCategory.Sound ? BadSound : BadVideo;
                return bad[index % bad.Length];
            }
            var pool = TryFind(r.TargetId, out var e) ? e.comments : CategoryComments[(int)r.Category];
            return pool[(index + (int)r.Grade) % pool.Length];
        }
    }
}
