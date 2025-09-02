// server.js
import express from "express";

const app = express();
app.use(express.json());

// 🔑 API 키: 여러 이름 중 먼저 발견되는 걸 사용
const OPENAI_KEY =
  process.env.OPENAI_API_KEY ||
  process.env.Api_key ||
  process.env.API_KEY ||
  "";

if (!OPENAI_KEY) {
  console.warn(
    "[WARN] OpenAI API key not found. Set one of: OPENAI_API_KEY, Api_key, API_KEY"
  );
}

// 헬스체크
app.get("/ping", (req, res) => res.send("pong"));

app.post("/name-city", async (req, res) => {
  try {
    const { co2Tons, citizenSatisfaction, budget, topTags = [] } = req.body ?? {};

    // 태그 한글 라벨 (선택)
    const labelMap = {
      Factory: "공장",
      EcoPlant: "친환경 발전소(태양광 등)",
      EnergySaving: "에너지 절약형 주거",
      PublicTransport: "대중교통 시설",
      BikeRoad: "자전거 도로",
      school: "학교",
      hospital: "병원",
      park: "공원",
      EVcharger: "전기차 충전소",
      RecycleHub: "재활용 거점",
    };
    const humanTags = (topTags || []).map(t => labelMap[t] ?? t);

    if (!OPENAI_KEY) {
      // 키가 정말 없으면 즉시 에러 반환(원인 명확화)
      return res.status(500).json({ cityName: "에러", detail: "OPENAI_API_KEY/Api_key/API_KEY not set" });
    }

    // Chat Completions 사용 (파싱 쉬움)
    const messages = [
      { role: "system", content: "너는 게임 도시의 이름을 짓는 네이머다. 한국어, 2~4음절, 한 단어, 따옴표/특수문자/띄어쓰기 없이 답하라." },
      { role: "user", content:
        `지표: CO2=${co2Tons}t, 시민만족도=${citizenSatisfaction}, 예산=${budget}\n` +
        `도시 특징 태그: ${humanTags.join(", ")}\n` +
        `도시 이름 1개만 답하라.` }
    ];

    const r = await fetch("https://api.openai.com/v1/chat/completions", {
      method: "POST",
      headers: {
        "Authorization": `Bearer ${OPENAI_KEY}`,
        "Content-Type": "application/json",
      },
      body: JSON.stringify({
        model: "gpt-4o-mini",
        messages,
        temperature: 0.7,
      }),
    });

    const raw = await r.text();
    console.log("[OpenAI status]", r.status);
    if (!r.ok) {
      console.error("[OpenAI body]", raw);
      return res.status(500).json({ cityName: "에러", detail: `openai ${r.status}` });
    }

    const data = JSON.parse(raw);
    let name = data?.choices?.[0]?.message?.content?.trim() || "이름생성실패";

    // 후처리: 따옴표/공백 제거
    name = name.replace(/["'‘’“”\s]/g, "");
    if (!name) name = "이름생성실패";

    return res.json({ cityName: name });
  } catch (err) {
    console.error("[/name-city error]", err);
    return res.status(500).json({ cityName: "에러" });
  }
});

const PORT = process.env.PORT || 8000;
app.listen(PORT, () => {
  console.log(`name-city server on :${PORT}`);
});
