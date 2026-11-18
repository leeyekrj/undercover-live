namespace UndercoverLive
{
    /// <summary>
    /// 대상별 채팅·업로드 댓글. 종류(분류)만 보고 고르면 와이어 클립에 "사람 때린 거 맞죠??" 같은 엉뚱한 반응이 붙어서,
    /// 대상 id로 먼저 찾고 없을 때만 분류 공용 문구로 돌아간다. id는 앞부분만 맞으면 된다(cctv_0·cctv_1 → cctv).
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
                idPrefix = "miracle_wire",
                live = new[] { "줄 보인다 줄!!", "와이어 ㅋㅋㅋㅋ 공중부양 실체", "머리 위에 줄 걸려 있는 거 다 보임", "눈 가린 사람들만 기적 보는 거였네", "이거 클립 따야 됨" },
                comments = new[] {
                    new[] { "와이어 다 보인다 ㅋㅋ 공중부양 실체", "눈 가린 사람들만 믿는 기적이었네" },
                    new[] { "교주 머리 위에 줄 걸린 거 보임?", "이거 퍼가야 함 박제 각" },
                },
            },
            new Entry {
                idPrefix = "assistant",
                live = new[] { "커튼 뒤에 사람 있음", "저 사람이 조명 켜는 거임?", "기적이 아니라 조명 쇼네", "무대 감독까지 있는 사이비 ㄷㄷ" },
                comments = new[] {
                    new[] { "커튼 뒤에서 조명 돌리는 사람 찍힘", "기적이 아니라 조명 쇼였네" },
                    new[] { "빛 켜는 타이밍 맞추는 거 봐", "무대 감독까지 있는 사이비;;" },
                },
            },
            new Entry {
                idPrefix = "cctv",
                live = new[] { "천장에 빨간 불 저거 뭐임?", "카메라다 카메라", "신도들 다 찍히고 있었네", "폰으로 보니까 보이는 거 소름" },
                comments = new[] {
                    new[] { "천장 빨간 불 저거 감시 카메라임", "신도들 다 찍히고 있었네 소름" },
                    new[] { "적외선이라 맨눈으론 안 보인대", "폰 카메라로만 보이는 거 신기하다" },
                },
            },
            new Entry {
                idPrefix = "banner",
                live = new[] { "현수막 번역 실화냐", "'모든 돈을 바친다' ㅋㅋㅋ", "겉이랑 속이 완전 다르네", "저거 캡처해 둬" },
                comments = new[] {
                    new[] { "현수막 속뜻 '모든 돈을 바친다' 실화냐", "겉이랑 속이 완전 다르네" },
                    new[] { "번역 켜니까 본심 나오는 거 봐", "이거 캡처해서 박제해야 함" },
                },
            },
            new Entry {
                idPrefix = "leader",
                live = new[] { "눈동자님 ㅋㅋ 호칭부터 소름", "교주 말투 너무 차분해서 무서움", "의자도 혼자 하얗네", "교주 얼굴 나왔다" },
                comments = new[] {
                    new[] { "눈동자님 ㅋㅋ 호칭부터 소름", "말투가 차분해서 더 무서움" },
                    new[] { "혼자 하얀 의자에 앉아 있는 거 봐", "초상화까지 걸어 놨네" },
                },
            },
            new Entry {
                idPrefix = "crowd",
                live = new[] { "다들 눈가리개 쓰고 있음;;", "앞사람들 하나도 안 움직여", "다 같이 같은 말 하는 거 소름", "보는 내가 숨막힌다" },
                comments = new[] {
                    new[] { "다들 눈가리개 쓰고 있는 거 무섭다", "앞사람들 하나도 안 움직여;;" },
                    new[] { "다 같이 같은 말 외우는 거 소름", "보는 내가 다 숨막혀" },
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
