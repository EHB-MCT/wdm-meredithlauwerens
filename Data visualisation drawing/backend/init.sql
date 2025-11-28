CREATE TABLE strokes (
    id SERIAL PRIMARY KEY,
    uid TEXT NOT NULL,
    color TEXT NOT NULL,
    duration NUMERIC(10,2) NOT NULL,
    points JSONB NOT NULL
);

CREATE TABLE drawings (
    id SERIAL PRIMARY KEY,
    uid TEXT NOT NULL,
    total_duration NUMERIC(10,2) NOT NULL,
    strokes JSONB NOT NULL,
    erase_count INT DEFAULT 0,
    undo_count INT DEFAULT 0
);



