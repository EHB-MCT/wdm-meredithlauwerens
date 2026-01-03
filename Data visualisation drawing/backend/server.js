//a simple express API that captures and stores Unity data in PostgreSQL

import express from "express";
import bodyParser from "body-parser";
import cors from "cors";
import pkg from "pg";

const { Pool } = pkg;

const app = express();
app.use(cors());
app.use(bodyParser.json());

//database connection via environment variable
const pool = new Pool({
	connectionString: process.env.DATABASE_URL,
});

const round2 = (n) => Math.round(Number(n) * 100) / 100;

//test endpoint
app.get("/", (req, res) => {
	res.send("Drawing API is running!");
});

//receive Unity-data
app.post("/api/strokes", async (req, res) => {
	try {
		const { uid, color, duration, bounds } = req.body;

		if (!uid || !color || !bounds) {
			return res.status(400).send("Missing required fields");
		}

		const roundedDuration = round2(duration);

		await pool.query("INSERT INTO strokes (uid, color, duration, bounds) VALUES ($1, $2, $3, $4)", [uid, color, roundedDuration, JSON.stringify(bounds)]);

		const width = bounds.maxX - bounds.minX;
		const height = bounds.maxY - bounds.minY;
		console.log(`Saved stroke: uid=${uid}, color=${color}, duration=${roundedDuration}, width=${width}, height=${height}`);

		res.status(200).send("Data saved");
	} catch (err) {
		console.error("Error saving data:", err);
		res.status(500).send("Database error");
	}
});

app.post("/api/done", async (req, res) => {
	const { uid, done } = req.body;

	if (!uid || !done) {
		return res.status(400).send("Missing fields");
	}

	console.log(`User ${uid} finished drawing`);

	res.status(200).send("Done received");
});

app.post("/api/drawing", async (req, res) => {
	console.log("/api/drawing HIT");

	const { uid, totalDuration, strokes, colorChangeCount, eraseCount, undoCount, redoCount, increaseWidthCount, decreaseWidthCount } = req.body;

	const roundedTotalDuration = Math.round(totalDuration * 100) / 100;

	const normalizedStrokes = (strokes || []).map((s) => ({
		x: s.x,
		y: s.y,
		t: s.timestamp,
	}));

	try {
		await pool.query(
			`INSERT INTO drawings (
        uid,
        total_duration,
        stroke_count,
        strokes,
        color_change_count,
        erase_count,
        undo_count,
        redo_count,
        increase_width_count,
        decrease_width_count
      ) VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10)`,
			[uid, roundedTotalDuration, normalizedStrokes.length, JSON.stringify(normalizedStrokes), colorChangeCount || 0, eraseCount || 0, undoCount || 0, redoCount || 0, increaseWidthCount || 0, decreaseWidthCount || 0]
		);

		console.log("DRAWING INSERTED for uid:", uid);
		res.sendStatus(200);
	} catch (err) {
		console.error("DRAWING INSERT FAILED:", err);
		res.sendStatus(500);
	}
});

app.post("/api/session", async (req, res) => {
	try {
		const { uid, session } = req.body;

		if (!uid || !session) {
			console.warn("Missing uid or session in payload");
			return res.status(400).send("Missing fields");
		}

		const normalizedSession = session.map((topicData) => {
			const rawStrokes = topicData.drawing?.strokes || [];

			const normalizedStrokes = rawStrokes.map((s) => ({
				uid,
				color: s.color,
				duration: round2(s.duration),
				bounds: s.bounds || null,
			}));

			return {
				...topicData,
				drawing: topicData.drawing
					? {
							...topicData.drawing,
							totalDuration: round2(topicData.drawing.totalDuration),
							strokeCount: normalizedStrokes.length,
							strokes: normalizedStrokes,
					  }
					: null,
			};
		});

		console.log("Received session data:\n", JSON.stringify({ uid, session: normalizedSession }, null, 2));

		for (const topicData of normalizedSession) {
			const strokes = topicData.drawing?.strokes || [];

			await pool.query(
				`INSERT INTO topic_drawings (uid, topic, used_reference, strokes, stroke_count)
   VALUES ($1, $2, $3, $4, $5)`,
				[uid, topicData.topic, topicData.usedReference, JSON.stringify(strokes), topicData.drawing.strokeCount]
			);

			if (!topicData.drawing || strokes.length === 0) {
				console.warn(`Topic "${topicData.topic}" has no drawing data for user ${uid}.`);
			}
		}

		res.status(200).send("Session saved");
	} catch (err) {
		console.error("Error saving session:", err);
		res.status(500).send("Database error");
	}
});

/*CHARTS*/
//get all users
app.get("/api/admin/users", async (req, res) => {
	const result = await pool.query("SELECT DISTINCT uid FROM topic_drawings");
	res.json(result.rows);
});

//get all drawings for a user
app.get("/api/admin/user/:uid", async (req, res) => {
	const { uid } = req.params;

	const drawings = await pool.query("SELECT * FROM drawings WHERE uid = $1 ORDER BY id", [uid]);

	const topics = await pool.query("SELECT * FROM topic_drawings WHERE uid = $1", [uid]);

	res.json({
		drawings: drawings.rows,
		topics: topics.rows,
	});
});

//aggregated statistics
app.get("/api/admin/stats", async (req, res) => {
	const result = await pool.query(`
    SELECT
      COUNT(DISTINCT uid) as users,
      AVG(total_duration) as avg_duration,
      AVG(stroke_count) as avg_strokes,
      AVG(erase_count) as avg_erases,
      AVG(undo_count) as avg_undos
    FROM drawings
  `);

	res.json(result.rows[0]);
});

app.get("/api/admin/user/:uid/colors", async (req, res) => {
	const { uid } = req.params;

	const result = await pool.query(
		`SELECT color, COUNT(*) AS count
     FROM strokes
     WHERE uid = $1
     GROUP BY color`,
		[uid]
	);

	res.json(result.rows);
});

app.get("/api/admin/user/:uid/strokes-per-topic", async (req, res) => {
	const { uid } = req.params;

	const result = await pool.query(
		`SELECT topic, stroke_count
   FROM topic_drawings
   WHERE uid = $1`,
		[uid]
	);

	res.json(result.rows);
});

app.get("/api/admin/user/:uid/bounds", async (req, res) => {
	const { uid } = req.params;

	try {
		const result = await pool.query(
			`
      SELECT
        td.topic,
        COALESCE(
          SUM(
            ((stroke->'bounds'->>'maxX')::float - (stroke->'bounds'->>'minX')::float)
            *
            ((stroke->'bounds'->>'maxZ')::float - (stroke->'bounds'->>'minZ')::float)
          ),
          0
        ) AS total_area
      FROM topic_drawings td
      LEFT JOIN LATERAL jsonb_array_elements(td.strokes) AS stroke ON true
      WHERE td.uid = $1
      GROUP BY td.topic
      ORDER BY td.topic;
    `,
			[uid]
		);

		res.json(result.rows);
	} catch (err) {
		console.error("Error fetching bounds chart:", err);
		res.status(500).send("Database error");
	}
});

const PORT = 5000;
app.listen(PORT, "0.0.0.0", () => {
	console.log(` Server running on http://0.0.0.0:${PORT}`);
});
