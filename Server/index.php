<?php
header('Content-Type: application/json');
header('Access-Control-Allow-Origin: *');
header('Access-Control-Allow-Methods: GET, POST, OPTIONS');
header('Access-Control-Allow-Headers: Content-Type');

require_once 'User.php';
require_once 'Instance.php';

function string_to_hash(string $str): int {
    $hash = 0;
    $len = strlen($str);

    for ($i = 0; $i < $len; $i++) 
        $hash = ($hash * 31 + ord($str[$i])) & 0xFFFFFFFF;
    
    if ($hash >= 0x80000000) 
        $hash -= 0x100000000;

    return $hash;
}

if ($_SERVER['REQUEST_METHOD'] === 'OPTIONS') {
    http_response_code(200);
    exit();
}

$requestUri = parse_url($_SERVER['REQUEST_URI'], PHP_URL_PATH);
$requestMethod = $_SERVER['REQUEST_METHOD'];

if ($requestMethod !== 'GET') {
    http_response_code(405);
    echo json_encode(['error' => 'Method Not Allowed']);
    exit();
}

$ip = $_SERVER['HTTP_X_FORWARDED_FOR'] 
    ?? $_SERVER['HTTP_X_REAL_IP'] 
    ?? $_SERVER['REMOTE_ADDR'] 
    ?? 'unknown';
if (strpos($ip, ',') !== false) 
    $ip = trim(explode(',', $ip)[0]);
$uid = string_to_hash($ip);
$time = round(microtime(true) * 1000);

try {
    $user = User::findOrCreate($uid);
} catch (Exception $e) {
    error_log("Erreur lors de la gestion de l'utilisateur: " . $e->getMessage());
    http_response_code(500);
    echo json_encode(['error' => 'Erreur serveur']);
    exit();
}

// Vérifier si l'URI correspond au pattern /aj<base36_number>
if (preg_match('/^\/aj([0-9a-z]+)$/i', $requestUri, $matches)) {
    $base36Number = $matches[1];
    $instanceId = base_convert($base36Number, 36, 10);
    
    // Vérifier que l'ID est dans la plage valide (0-63)
    if ($instanceId < 0 || $instanceId > 63) {
        http_response_code(400);
        echo json_encode(['error' => 'ID d\'instance invalide (doit être entre 0 et 63)']);
        exit();
    }
    
    try {
        $instance = Instance::find($instanceId);
        
        if ($instance === null) {
            http_response_code(404);
            echo json_encode(['error' => 'Instance non trouvée']);
        } else {
            $instance->addPlayer($user);
            echo json_encode([
                'time' => $time,
                'uid' => $uid,
                'instance' => $instance->toArray(),
                'user' => $user->toArray()
            ]);
        }
    } catch (Exception $e) {
        error_log("Erreur lors de la jointure à l'instance: " . $e->getMessage());
        http_response_code(500);
        echo json_encode(['error' => 'Erreur lors de la jointure à l\'instance']);
    }
} else switch ($requestUri) {
    case '/au':
        echo json_encode([
            'time' => $time,
            'uid' => $uid,
            'user' => $user->toArray()
        ]);
        break;
    case '/ac':
        try {
            $instance = Instance::createWithRotation($user);
            echo json_encode([
                'time' => $time,
                'uid' => $uid,
                'instance' => $instance->toArray(),
                'user' => $user->toArray()
            ]);
        } catch (Exception $e) {
            error_log("Erreur lors de la création de l'instance: " . $e->getMessage());
            http_response_code(500);
            echo json_encode(['error' => 'Erreur lors de la création de l\'instance']);
        }
        break;
    default:
        http_response_code(404);
        echo json_encode(['error' => 'Not Found']);
        break;
}
?>
