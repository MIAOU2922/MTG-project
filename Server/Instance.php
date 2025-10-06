<?php

require_once 'Database.php';

class Instance {
    public $id;
    public $created_at;
    public $last_seen;
    public $players; // JSON des joueurs

    public function __construct($id = null, $created_at = null, $last_seen = null, $players = null) {
        $this->id = $id;
        $this->created_at = $created_at;
        $this->last_seen = $last_seen;
        $this->players = $players ?? '[]';
    }

    /**
     * Trouve une instance par son ID
     * @param int $id L'ID de l'instance à chercher
     * @return Instance|null L'instance trouvée ou null si non trouvée
     */
    public static function find(int $id): ?Instance {
        try {
            $db = Database::getInstance();
            $stmt = $db->query("SELECT * FROM instances WHERE id = ?", [$id]);
            $instanceData = $stmt->fetch();
            
            if ($instanceData) {
                return new Instance(
                    $instanceData['id'],
                    $instanceData['created_at'],
                    $instanceData['last_seen'],
                    $instanceData['players']
                );
            }
            
            return null;
        } catch (Exception $e) {
            error_log("Erreur lors de la recherche de l'instance: " . $e->getMessage());
            return null;
        }
    }

    /**
     * Compte le nombre d'instances existantes
     * @return int Le nombre d'instances
     */
    public static function count(): int {
        try {
            $db = Database::getInstance();
            $stmt = $db->query("SELECT COUNT(*) as count FROM instances");
            $result = $stmt->fetch();
            return intval($result['count']);
        } catch (Exception $e) {
            error_log("Erreur lors du comptage des instances: " . $e->getMessage());
            return 0;
        }
    }

    /**
     * Trouve l'instance avec le plus ancien last_seen
     * @return Instance|null L'instance la plus ancienne ou null
     */
    public static function findOldest(): ?Instance {
        try {
            $db = Database::getInstance();
            $stmt = $db->query("SELECT * FROM instances ORDER BY last_seen ASC LIMIT 1");
            $instanceData = $stmt->fetch();
            
            if ($instanceData) {
                return new Instance(
                    $instanceData['id'],
                    $instanceData['created_at'],
                    $instanceData['last_seen'],
                    $instanceData['players']
                );
            }
            
            return null;
        } catch (Exception $e) {
            error_log("Erreur lors de la recherche de l'instance la plus ancienne: " . $e->getMessage());
            return null;
        }
    }

    /**
     * Trouve un ID libre entre 0 et 63
     * @return int|null Un ID libre ou null si tous sont pris
     */
    public static function findFreeId(): ?int {
        try {
            $db = Database::getInstance();
            
            for ($id = 0; $id < 64; $id++) {
                $stmt = $db->query("SELECT COUNT(*) as count FROM instances WHERE id = ?", [$id]);
                $result = $stmt->fetch();
                
                if (intval($result['count']) === 0) {
                    return $id;
                }
            }
            
            return null; // Tous les IDs sont pris
        } catch (Exception $e) {
            error_log("Erreur lors de la recherche d'ID libre: " . $e->getMessage());
            return null;
        }
    }

    /**
     * Crée une nouvelle instance avec rotation automatique
     * @param User $user L'utilisateur à ajouter à l'instance
     * @return Instance La nouvelle instance créée
     */
    public static function createWithRotation(User $user): Instance {
        try {
            $db = Database::getInstance();
            $now = round(microtime(true) * 1000);
            
            // Chercher un ID libre
            $freeId = self::findFreeId();
            
            if ($freeId === null) {
                // Pas d'ID libre, supprimer l'instance la plus ancienne
                $oldestInstance = self::findOldest();
                if ($oldestInstance) {
                    $oldestInstance->delete();
                    $freeId = $oldestInstance->id;
                } else {
                    throw new Exception("Impossible de trouver une instance à supprimer");
                }
            }
            
            // Créer la nouvelle instance avec l'utilisateur
            $players = json_encode([$user->toArray()]);
            
            $stmt = $db->query(
                "INSERT INTO instances (id, created_at, last_seen, players) VALUES (?, ?, ?, ?)",
                [$freeId, $now, $now, $players]
            );
            
            return new Instance($freeId, $now, $now, $players);
        } catch (Exception $e) {
            error_log("Erreur lors de la création de l'instance: " . $e->getMessage());
            throw $e;
        }
    }

    /**
     * Ajoute un joueur à l'instance
     * @param User $user L'utilisateur à ajouter
     * @return bool True si l'ajout a réussi
     */
    public function addPlayer(User $user): bool {
        try {
            $db = Database::getInstance();
            $now = round(microtime(true) * 1000);
            $players = json_decode($this->players, true);
            foreach ($players as $player) {
                if ($player['id'] === $user->id) {
                    $this->updateLastSeen();
                    return true;
                }
            }
            
            $players[] = $user->toArray();
            $this->players = json_encode($players);
            $stmt = $db->query(
                "UPDATE instances SET players = ?, last_seen = ? WHERE id = ?",
                [$this->players, $now, $this->id]
            );
            $this->last_seen = $now;
            return true;
        } catch (Exception $e) {
            error_log("Erreur lors de l'ajout du joueur: " . $e->getMessage());
            return false;
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
                "UPDATE instances SET last_seen = ? WHERE id = ?",
                [$now, $this->id]
            );
            $this->last_seen = $now;
            return true;
        } catch (Exception $e) {
            error_log("Erreur lors de la mise à jour du last_seen: " . $e->getMessage());
            return false;
        }
    }

    /**
     * Supprime l'instance
     * @return bool True si la suppression a réussi
     */
    public function delete(): bool {
        try {
            $db = Database::getInstance();
            $stmt = $db->query("DELETE FROM instances WHERE id = ?", [$this->id]);
            return true;
        } catch (Exception $e) {
            error_log("Erreur lors de la suppression de l'instance: " . $e->getMessage());
            return false;
        }
    }

    /**
     * Convertit l'instance en tableau associatif
     * @return array Représentation en tableau de l'instance
     */
    public function toArray(): array {
        return [
            'id' => $this->id,
            'created_at' => $this->created_at,
            'last_seen' => $this->last_seen,
            'players' => json_decode($this->players, true)
        ];
    }

    /**
     * Récupère toutes les instances
     * @return array Tableau d'objets Instance
     */
    public static function all(): array {
        try {
            $db = Database::getInstance();
            $stmt = $db->query("SELECT * FROM instances ORDER BY last_seen DESC");
            $instances = [];
            while ($instanceData = $stmt->fetch()) 
                $instances[] = new Instance(
                    $instanceData['id'],
                    $instanceData['created_at'],
                    $instanceData['last_seen'],
                    $instanceData['players']
                );
            return $instances;
        } catch (Exception $e) {
            error_log("Erreur lors de la récupération des instances: " . $e->getMessage());
            return [];
        }
    }
}
?>