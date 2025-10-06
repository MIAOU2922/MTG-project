<?php

class Database {
    private static $instance = null;
    private $connection;
    private $dbPath;

    private function __construct() {
        $this->dbPath = __DIR__ . '/database.sqlite';
        $this->connect();
        $this->initializeTables();
    }

    public static function getInstance(): Database {
        if (self::$instance === null) 
            self::$instance = new self();
        return self::$instance;
    }

    private function connect(): void {
        try {
            $this->connection = new PDO('sqlite:' . $this->dbPath);
            $this->connection->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
            $this->connection->setAttribute(PDO::ATTR_DEFAULT_FETCH_MODE, PDO::FETCH_ASSOC);
        } catch (PDOException $e) {
            throw new Exception('Database connection failed: ' . $e->getMessage());
        }
    }

    private function initializeTables(): void {
        // Table des utilisateurs
        $sqlUsers = "CREATE TABLE IF NOT EXISTS users (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            created_at BIGINT NOT NULL,
            lastseen_at BIGINT NOT NULL
        )";
        
        // Table des instances
        $sqlInstances = "CREATE TABLE IF NOT EXISTS instances (
            id INTEGER PRIMARY KEY,
            created_at BIGINT NOT NULL,
            last_seen BIGINT NOT NULL,
            players TEXT NOT NULL DEFAULT '[]'
        )";
        
        $this->connection->exec($sqlUsers);
        $this->connection->exec($sqlInstances);
        
        // Index pour optimiser la recherche de l'instance la plus ancienne
        $this->connection->exec("CREATE INDEX IF NOT EXISTS idx_instances_last_seen ON instances(last_seen)");
    }

    public function getConnection(): PDO {
        return $this->connection;
    }

    public function query(string $sql, array $params = []): PDOStatement {
        $stmt = $this->connection->prepare($sql);
        $stmt->execute($params);
        return $stmt;
    }

    public function lastInsertId(): string {
        return $this->connection->lastInsertId();
    }
}
?>