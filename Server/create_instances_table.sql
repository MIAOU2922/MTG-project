-- Script SQL pour créer la table des instances
CREATE TABLE IF NOT EXISTS instances (
    id INT PRIMARY KEY,
    created_at BIGINT NOT NULL,
    last_seen BIGINT NOT NULL,
    players TEXT NOT NULL DEFAULT '[]'
);

-- Index pour optimiser la recherche de l'instance la plus ancienne
CREATE INDEX IF NOT EXISTS idx_instances_last_seen ON instances(last_seen);