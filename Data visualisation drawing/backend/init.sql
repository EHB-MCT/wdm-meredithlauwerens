CREATE TABLE strokes (
    id SERIAL PRIMARY KEY,
    uid TEXT NOT NULL,
    color TEXT NOT NULL,
    duration NUMERIC(10,2) NOT NULL,
    bounds JSONB NOT NULL
);

CREATE TABLE drawings (
    id SERIAL PRIMARY KEY,
    uid TEXT NOT NULL,
    total_duration NUMERIC(10,2) NOT NULL,
    stroke_count INT,
    strokes JSONB NOT NULL,
    color_change_count INT DEFAULT 0,
    erase_count INT DEFAULT 0,
    undo_count INT DEFAULT 0,
    redo_count INT DEFAULT 0,
    increase_width_count INT DEFAULT 0,
    decrease_width_count INT DEFAULT 0
);

CREATE TABLE topic_drawings (
    id SERIAL PRIMARY KEY,
    uid TEXT NOT NULL,
    topic TEXT NOT NULL,
    used_reference BOOLEAN NOT NULL,
    strokes JSONB NOT NULL,
    stroke_count INT
);

CREATE TABLE events (
    id SERIAL PRIMARY KEY,
    uid TEXT,
    event_type TEXT,
    event_data JSONB,
    created_at TIMESTAMP DEFAULT NOW()
);

CREATE TABLE sessions (
    id UUID PRIMARY KEY,
    uid TEXT,
    started_at TIMESTAMP,
    ended_at TIMESTAMP
);




