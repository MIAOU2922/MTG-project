<?php

require_once 'Database.php';

class User {
    public $id;
    public $created_at;
    public $lastseen_at;

    public function __construct($id = null, $created_at = null, $lastseen_at = null) {
        $this->id = $id;
        $this->created_at = $created_at;
        $this->lastseen_at = $lastseen_at;
    }

    /**
     * Trouve un utilisateur par son ID
     * @param int $id L'ID de l'utilisateur à chercher
     * @return User|null L'utilisateur trouvé ou null si non trouvé
     */
    public static function find(int $id): ?User {
        try {
            $db = Database::getInstance();
            $stmt = $db->query("SELECT * FROM users WHERE id = ?", [$id]);
            $userData = $stmt->fetch();
            
            if ($userData) {
                return new User(
                    $userData['id'],
                    $userData['created_at'],
                    $userData['lastseen_at']
                );
            }
            
            return null;
        } catch (Exception $e) {
            error_log("Erreur lors de la recherche de l'utilisateur: " . $e->getMessage());
            return null;
        }
    }

    /**
     * Trouve ou crée un utilisateur avec un ID spécifique
     * @param int $id L'ID de l'utilisateur
     * @return User L'utilisateur trouvé ou créé
     */
    public static function findOrCreate(int $id): User {
        $user = self::find($id);
        
        if ($user === null) {
            $user = self::create($id);
        } else {
            // Met à jour le lastseen_at
            $user->updateLastSeen();
        }
        
        return $user;
    }

    /**
     * Crée un nouvel utilisateur
     * @param int|null $id ID spécifique (optionnel, auto-increment sinon)
     * @return User Le nouvel utilisateur créé
     */
    public static function create(int $id = null): User {
        try {
            $db = Database::getInstance();
            $now = round(microtime(true) * 1000);
            
            if ($id !== null) {
                // Crée avec un ID spécifique
                $stmt = $db->query(
                    "INSERT INTO users (id, created_at, lastseen_at) VALUES (?, ?, ?)",
                    [$id, $now, $now]
                );
                return new User($id, $now, $now);
            } else {
                // Crée avec auto-increment
                $stmt = $db->query(
                    "INSERT INTO users (created_at, lastseen_at) VALUES (?, ?)",
                    [$now, $now]
                );
                $newId = intval($db->lastInsertId());
                return new User($newId, $now, $now);
            }
        } catch (Exception $e) {
            error_log("Erreur lors de la création de l'utilisateur: " . $e->getMessage());
            throw $e;
        }
    }

    /**
     * Met à jour le timestamp de dernière activité
     * @return bool True si la mise à jour a réussi
     */
    public function updateLastSeen(): bool {
        try {
            $db = Database::getInstance();
            $now = round(microtime(true) * 1000);
            
            $stmt = $db->query(
                "UPDATE users SET lastseen_at = ? WHERE id = ?",
                [$now, $this->id]
            );
            
            $this->lastseen_at = $now;
            return true;
        } catch (Exception $e) {
            error_log("Erreur lors de la mise à jour du lastseen: " . $e->getMessage());
            return false;
        }
    }

    /**
     * Récupère tous les utilisateurs
     * @return array Tableau d'objets User
     */
    public static function all(): array {
        try {
            $db = Database::getInstance();
            $stmt = $db->query("SELECT * FROM users ORDER BY created_at DESC");
            $users = [];
            
            while ($userData = $stmt->fetch()) {
                $users[] = new User(
                    $userData['id'],
                    $userData['created_at'],
                    $userData['lastseen_at']
                );
            }
            
            return $users;
        } catch (Exception $e) {
            error_log("Erreur lors de la récupération des utilisateurs: " . $e->getMessage());
            return [];
        }
    }

    /**
     * Supprime un utilisateur
     * @return bool True si la suppression a réussi
     */
    public function delete(): bool {
        try {
            $db = Database::getInstance();
            $stmt = $db->query("DELETE FROM users WHERE id = ?", [$this->id]);
            return true;
        } catch (Exception $e) {
            error_log("Erreur lors de la suppression de l'utilisateur: " . $e->getMessage());
            return false;
        }
    }

    /**
     * Convertit l'utilisateur en tableau associatif
     * @return array Représentation en tableau de l'utilisateur
     */
    public function toArray(): array {
        return [
            'id' => $this->id,
            'created_at' => $this->created_at,
            'lastseen_at' => $this->lastseen_at
        ];
    }

    /**
     * Convertit l'utilisateur en JSON
     * @return string Représentation JSON de l'utilisateur
     */
    public function toJson(): string {
        return json_encode($this->toArray());
    }

    /**
     * Vérifie si l'utilisateur existe en base de données
     * @return bool True si l'utilisateur existe
     */
    public function exists(): bool {
        return self::find($this->id) !== null;
    }
}
?>