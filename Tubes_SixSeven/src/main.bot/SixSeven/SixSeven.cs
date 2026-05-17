using System;
using System.Collections.Generic;
using System.Drawing;
using Robocode.TankRoyale.BotApi;
using Robocode.TankRoyale.BotApi.Events;

public class SixSeven : Bot
{   
    // === KONSTANTA DAN VARIABEL GLOBAL ===
    private const double RADIAN_CONVERSION = Math.PI / 180;
    private Dictionary<int, EnemyData> enemyRegistry; // Dictionary guna menyimpan data musuh saat scan
    private EnemyData primaryTarget; // Target saat ini
    private PointD currentPosition, destinationPoint; // Posisi saat ini dan tujuan berikutnya
    
    // Konfigurasi kekuatan tembakan
    private double LOWEST_POWER = 0.1;
    private double HIGHEST_POWER = 3.0;
    private double REFERENCE_RANGE = 150.0;
    
    // Counter untuk metode gerak stop and go
    private int motionCycleTick = 0;
    private int currentCycleDuration = 0;
    
    // Interval gerak dan diam saat 1v1
    private const int MIN_MOVE_DURATION = 30;
    private const int MAX_MOVE_DURATION = 180;
    private const int MIN_STOP_DURATION = 5;
    private const int MAX_STOP_DURATION = 10;
    private bool isInMotion = true;
    private static Random randomizer = new Random();
    
    static void Main(string[] args)
    {
        new SixSeven().Start();
    }

    public SixSeven() : base(BotInfo.FromFile("SixSeven.json")) { }

    public override void Run()
    {
        // Adjust flags: memungkinkan radar dan gun berputar independen dari tubuh bot
        ConfigureBotComponents();
        
        // Inisialisasi semua data bot
        InitializeBotData();
        
        // Batasan arena (diberi margin 45 unit dari tepi)
        RectangleD arenaBounds = new RectangleD(45, 45, ArenaWidth - 90, ArenaHeight - 90);
        
        // Inisialisasi interval random untuk stop-and-go movement
        currentCycleDuration = randomizer.Next(MIN_MOVE_DURATION, MAX_MOVE_DURATION + 1);
        
        while (IsRunning)
        {
            // === GREEDY SELECTION 1: Memutar radar ===
            // Memutar radar terus menerus agar tidak kehilangan jejak musuh
            // (sudut pemindaian harus >0 derajat agar bisa mendeteksi)
            ExecuteRadarScanning();
            
            // === GREEDY SELECTION 2: Cari musuh yang paling lama tidak discan ===
            // Fungsi seleksi: prioritaskan musuh dengan scanTime terkecil
            TrackStaleEnemies();
            
            // === GREEDY SELECTION 3: Pilih target terdekat ===
            SelectNearestEnemy();
            
            // update posisi saat ini
            UpdateCurrentPosition();
            
            // === GREEDY SELECTION 4: Tentukan pergerakan ===
            // Delay 10 turn di awal agar radar sempat mendeteksi musuh terlebih dahulu
            if (TurnNumber > 9 && IsTargetActive())
            {
                ExecuteMovementStrategy(arenaBounds);
            }
            
            // Panggil Go() untuk mengeksekusi semua perintah movement yang sudah di-set
            Go();
        }
    }
    
    // ==================== KONFIGURASI BOT ====================
    /// Mengatur komponen bot (radar, gun, body agar bisa berputar independen)
    private void ConfigureBotComponents()
    {
        AdjustGunForBodyTurn = true;
        AdjustRadarForGunTurn = true;
        AdjustRadarForBodyTurn = true;
        SetBotColors();
    }
    
    /// Mengatur warna-warna bot (theme pink/magenta)
    private void SetBotColors()
    {
        BodyColor = Color.FromArgb(255, 209, 220);
        TurretColor = Color.FromArgb(255, 182, 193);
        RadarColor = Color.FromArgb(255, 240, 245);
        BulletColor = Color.FromArgb(255, 20, 147);
        ScanColor = Color.FromArgb(255, 228, 236);
        TracksColor = Color.FromArgb(199, 120, 150);
        GunColor = Color.FromArgb(255, 192, 203);
    }
    
    /// Inisialisasi data awal bot
    private void InitializeBotData()
    {
        primaryTarget = new EnemyData(0, new PointD(X, Y), 0, 0, 0, 0);
        primaryTarget.isAlive = false;  // target belum aktif karena belum ada musuh
        enemyRegistry = new Dictionary<int, EnemyData>();
        currentPosition = new PointD(X, Y);
        destinationPoint = currentPosition;
    }
    
    // ==================== RADAR SYSTEM ====================
    /// Eksekusi pergerakan radar untuk scanning
    private void ExecuteRadarScanning()
    {
        SetTurnRadarLeft(60);
    }
    
    /// Melacak musuh yang sudah lama tidak discan dan mengarahkan radar ke mereka
    private void TrackStaleEnemies()
    {
        try 
        {
            int oldestScanTime = int.MaxValue;
            foreach (EnemyData enemy in enemyRegistry.Values) 
            {
                if (TurnNumber > 20 && enemy.isAlive && enemy.lastSeen < oldestScanTime) 
                {
                    oldestScanTime = enemy.lastSeen;
                    double bearingAngle = CalculateRadarBearing(enemy.location.X, enemy.location.Y);
                    double turnAmount = Math.Sign(bearingAngle) * 60;
                    SetTurnRadarLeft(turnAmount); // blocking method
                }
            }
        } 
        catch (NullReferenceException e) 
        {
            // Biarkan lanjut, tidak perlu print setiap turn
        }
    }
    
    /// Menghitung sudut bearing radar ke koordinat target
    private double CalculateRadarBearing(double targetX, double targetY)
    {
        return RadarBearingTo(targetX, targetY);
    }
    
    // ==================== TARGET SELECTION ====================
    /// GREEDY SELECTION: Memilih musuh dengan jarak terdekat sebagai target
    /// Himpunan kandidat: semua musuh yang aktif
    /// Fungsi seleksi: jarak terdekat (memaksimalkan peluang tembakan tepat)
    /// Fungsi objektif: memaksimalkan bullet damage dan peluang kill
    private void SelectNearestEnemy()
    {
        double closestDistance = double.MaxValue;
        
        foreach (EnemyData enemy in enemyRegistry.Values)
        {
            if (!enemy.isAlive) continue;
            
            double distanceToEnemy = GetDistanceTo(enemy.location.X, enemy.location.Y);
            if (distanceToEnemy < closestDistance)
            {
                closestDistance = distanceToEnemy;
                primaryTarget = enemy;
                primaryTarget.isAlive = true;
            }
        }
    }
    
    /// Mengecek apakah target saat ini aktif
    private bool IsTargetActive()
    {
        return primaryTarget != null && primaryTarget.isAlive;
    }
    
    /// Memperbarui posisi bot saat ini
    private void UpdateCurrentPosition()
    {
        currentPosition.X = X;
        currentPosition.Y = Y;
    }
    
    /// Mendapatkan jarak ke target saat ini
    private double GetDistanceToTarget()
    {
        if (!IsTargetActive()) return double.MaxValue;
        return GetDistanceTo(primaryTarget.location.X, primaryTarget.location.Y);
    }
    
    // ==================== MOVEMENT STRATEGY ====================
    /// Mengatur pergerakan bot menggunakan strategi greedy:
    /// - Himpunan kandidat: semua titik di sekitar bot
    /// - Fungsi seleksi: pilih titik dengan nilai risiko terendah (RiskFunction)
    /// - Fungsi objektif: memaksimalkan survival dengan menghindari tembakan musuh
    private void ExecuteMovementStrategy(RectangleD arenaBounds)
    {
        bool isDuelMode = (EnemyCount == 1); // Deteksi apakah mode 1 vs 1
        
        // Pengecekan null untuk target
        if (primaryTarget == null || !primaryTarget.isAlive) return;
        
        bool isFarFromTarget = (GetDistanceToTarget() > 200);
        
        // ===== STRATEGI STOP-AND-GO (khusus 1vs1) =====
        // Tujuan: mengecoh musuh yang menggunakan linear targeting
        // Dengan berhenti tiba-tiba, prediksi posisi musuh menjadi meleset
        if (isDuelMode && isFarFromTarget)
        {
            ApplyOscillatingMovement(arenaBounds);
        }
        else
        {
            ApplyStandardNavigation(arenaBounds);
        }
    }
    
    /// Menerapkan gerak stop-and-go (bergerak lalu diam secara periodik)
    private void ApplyOscillatingMovement(RectangleD arenaBounds)
    {
        if (motionCycleTick >= currentCycleDuration) 
        {
            // Ganti status: bergerak <-> diam
            ToggleMovementState();
            motionCycleTick = 0;
            
            if (isInMotion) 
            {
                currentCycleDuration = randomizer.Next(MIN_MOVE_DURATION, MAX_MOVE_DURATION + 1);
            } 
            else 
            {
                currentCycleDuration = randomizer.Next(MIN_STOP_DURATION, MAX_STOP_DURATION + 1);
            }
        }
        
        if (isInMotion) 
        {
            MoveToDestination(arenaBounds);
        } 
        else 
        {
            SetForward(0); // Bot diam
        }
        
        motionCycleTick++;
    }
    
    /// Toggle status gerak (bergerak <-> diam)
    private void ToggleMovementState()
    {
        isInMotion = !isInMotion;
    }
    
    /// Navigasi standar (terus bergerak tanpa stop-and-go)
    private void ApplyStandardNavigation(RectangleD arenaBounds)
    {
        MoveToDestination(arenaBounds);
    }
    
    /// Bergerak menuju titik tujuan
    private void MoveToDestination(RectangleD arenaBounds)
    {
        double distanceRemaining = GetDistanceTo(destinationPoint.X, destinationPoint.Y);
        
        if (distanceRemaining < 15) 
        {
            // Mencari titik tujuan baru yang aman
            DetermineNewDestination(arenaBounds);
        } 
        else 
        {
            ExecuteMovementToPoint(distanceRemaining);
        }
    }
    
    /// Eksekusi pergerakan ke suatu titik dengan sudut tertentu
    private void ExecuteMovementToPoint(double distance)
    {
        double turnAngle = CalculateBearingAngle(destinationPoint.X, destinationPoint.Y);
        double movementDirection = 1;
        
        // Jika sudut > 90 derajat, lebih efisien mundur
        if (Math.Cos(RADIAN_CONVERSION * turnAngle) < 0) 
        {
            turnAngle -= 180;
            movementDirection = -1;
        }
        
        SetTurnLeft(turnAngle);
        SetForward(distance * movementDirection);
    }
    
    /// Menghitung sudut bearing ke koordinat target
    private double CalculateBearingAngle(double targetX, double targetY)
    {
        return BearingTo(targetX, targetY);
    }
    
    // ==================== WAYPOINT SELECTION ====================
    /// GREEDY SELECTION: Mencari titik tujuan terbaik di sekitar bot
    /// Himpunan kandidat: 360 titik dengan radius dan sudut bervariasi
    /// Fungsi seleksi: pilih titik dengan nilai RiskFunction terendah
    private void DetermineNewDestination(RectangleD arenaBounds)
    {
        double enemyRange = GetDistanceToTarget();
        PointD bestPoint = currentPosition;
        double lowestDanger = double.MaxValue;
        int iterations = 0;
        
        do 
        {
            double stepDistance = Math.Min(0.7 * enemyRange, 100 + 150 * randomizer.NextDouble());
            double randomDirection = 2 * Math.PI * randomizer.NextDouble();
            PointD candidatePoint = ProjectPoint(currentPosition, stepDistance, randomDirection);
            
            double dangerScore = CalculateDangerLevel(candidatePoint);
            bool isWithinBounds = arenaBounds.contains(candidatePoint.X, candidatePoint.Y);
            bool isPathClear = !IsMovementBlocked(currentPosition, candidatePoint);
            
            if (isWithinBounds && dangerScore < lowestDanger && isPathClear)
            {
                lowestDanger = dangerScore;
                bestPoint = candidatePoint;
                destinationPoint = bestPoint;
            }
            iterations++;
        } 
        while (iterations < 360);   // Mencoba 360 titik di sekitar bot
    }
    
    /// FUNGSI OBJEKTIF: Menghitung tingkat risiko suatu titik tujuan
    /// Semakin tinggi nilai = semakin berbahaya (banyak musuh di sekitar)
    /// Formula: risk = 0.08/distToCurrent^2 + sum( (energyRatio * (1 + perpendicularity)) / distToEnemy^2 )
    private double CalculateDangerLevel(PointD targetPosition)
    {
        // Risiko dasar: jangan diam di tempat (avoid getting stuck)
        double danger = 0.08 / GetSquaredDistance(targetPosition, currentPosition);
        
        foreach (EnemyData enemy in enemyRegistry.Values) 
        {
            if (!enemy.isAlive) continue;
            
            // Rasio energi: musuh berenergi tinggi lebih berbahaya
            double energyFactor = Math.Min(enemy.energy / Energy, 2);
            
            // Tegak lurus: musuh yang tegak lurus dengan jalur lebih berbahaya
            double perpendicularFactor = Math.Abs(Math.Cos(
                CalculateVectorAngle(currentPosition, targetPosition) - 
                CalculateVectorAngle(enemy.location, targetPosition)));
            
            // Jarak: semakin dekat semakin berbahaya (inverse square law)
            double distanceFactor = GetSquaredDistance(targetPosition, enemy.location);
            
            danger += energyFactor * (1 + perpendicularFactor) / Math.Max(distanceFactor, 1);
        }
        return danger;
    }
    
    /// Menghitung sudut vektor antara dua titik (dalam radian)
    private double CalculateVectorAngle(PointD origin, PointD target)
    {
        return Math.Atan2(target.Y - origin.Y, target.X - origin.X);
    }
    
    /// Menghitung titik baru dari titik awal, jarak, dan sudut
    private PointD ProjectPoint(PointD start, double distance, double angle)
    {
        return new PointD(start.X + distance * Math.Cos(angle), start.Y + distance * Math.Sin(angle));
    }
    
    // ==================== TARGETING SYSTEM ====================
    /// GREEDY SELECTION: Menentukan kekuatan tembakan optimal
    /// Himpunan kandidat: 0.1 - 3.0 (semua power yang diizinkan)
    /// Fungsi seleksi: pilih power terbesar yang tidak melanggar batasan
    /// Batasan: tidak menghabiskan energi sendiri dan tidak overkill musuh
    private double CalculateOptimalBulletPower()
    {
        double safeDistance = Math.Max(GetDistanceToTarget(), 1.0);
        
        // Komponen jarak: semakin dekat semakin besar power
        double distanceComponent = 0.65 + (REFERENCE_RANGE / safeDistance);
        // Jangan pakai power lebih dari 30% energi musuh (avoid overkill)
        double enemyPowerLimit = primaryTarget.energy * 0.3;
        // Jangan habiskan lebih dari 20% energi sendiri
        double selfPowerLimit = Energy * 0.2;
        
        // GREEDY: ambil power terbesar dari semua batasan
        double bulletPower = Math.Min(HIGHEST_POWER, 
            Math.Max(LOWEST_POWER, Math.Min(Math.Min(enemyPowerLimit, selfPowerLimit), distanceComponent)));
        
        // Saat energi sangat rendah, hemat energi (prioritas survival)
        if (Energy < 10) bulletPower = LOWEST_POWER;
        
        // Jarak sangat dekat (<150): full power
        if (GetDistanceToTarget() < 150 && IsTargetActive()) bulletPower = 3;
        
        return bulletPower;
    }
    
    /// Tentukan toleransi sudut tembakan (lebih longgar jika jarak dekat)
    private double GetAngleTolerance(double distanceToEnemy)
    {
        double MAX_TOLERANCE = 5.0;
        double CLOSE_RANGE = 50.0;
        double LONG_RANGE = 200.0;
        
        if (distanceToEnemy <= CLOSE_RANGE)
            return MAX_TOLERANCE;
        else if (distanceToEnemy <= LONG_RANGE)
            return MAX_TOLERANCE * ((LONG_RANGE - distanceToEnemy) / (LONG_RANGE - CLOSE_RANGE));
        else
            return 0.0;
    }
    
    /// LINEAR TARGETING: Memprediksi posisi musuh berdasarkan kecepatan konstan
    /// Menyelesaikan persamaan kuadrat untuk mencari waktu tempuh peluru
    /// Persamaan: |P_musuh_relatif + V_musuh * t| = bulletSpeed * t
    /// dimana P_musuh_relatif = posisi musuh relatif terhadap bot
    private void ExecuteLinearTargeting(double bulletPower, double angleTolerance)
    {
        // Jika musuh diam, cukup tembak langsung
        if (!IsTargetMoving()) 
        {
            ExecuteDirectTargeting(bulletPower, angleTolerance);
            return;
        }
        
        double distance = GetDistanceToTarget();
        double bulletSpeed = CalculateBulletSpeed(bulletPower);
        
        // Vektor kecepatan musuh
        double enemyVelocityX = primaryTarget.speed * Math.Cos(primaryTarget.direction * Math.PI / 180);
        double enemyVelocityY = primaryTarget.speed * Math.Sin(primaryTarget.direction * Math.PI / 180);
        
        // Vektor posisi relatif
        double deltaX = primaryTarget.location.X - X;
        double deltaY = primaryTarget.location.Y - Y;
        
        // a = |V_musuh|^2 - bulletSpeed^2
        // b = 2 * (P_musuh · V_musuh)
        // c = |P_musuh|^2
        double aCoeff = enemyVelocityX * enemyVelocityX + enemyVelocityY * enemyVelocityY - bulletSpeed * bulletSpeed;
        double bCoeff = 2 * (deltaX * enemyVelocityX + deltaY * enemyVelocityY);
        double cCoeff = deltaX * deltaX + deltaY * deltaY;
        double discriminant = bCoeff * bCoeff - 4 * aCoeff * cCoeff;
        
        double impactTime = 0;
        if (aCoeff != 0 && discriminant >= 0)
        {
            double t1 = (-bCoeff + Math.Sqrt(discriminant)) / (2 * aCoeff);
            double t2 = (-bCoeff - Math.Sqrt(discriminant)) / (2 * aCoeff);
            impactTime = (t1 > 0) ? t1 : (t2 > 0) ? t2 : 0;
        }
        else
        {
            impactTime = distance / bulletSpeed;
        }
        
        // Prediksi posisi musuh setelah waktu t
        double predictedX = primaryTarget.location.X + enemyVelocityX * impactTime;
        double predictedY = primaryTarget.location.Y + enemyVelocityY * impactTime;
        
        // Clamp ke batas arena
        predictedX = Math.Max(0, Math.Min(predictedX, ArenaWidth));
        predictedY = Math.Max(0, Math.Min(predictedY, ArenaHeight));
        
        // Arahkan gun ke posisi prediksi
        double gunAngle = GunBearingTo(predictedX, predictedY);
        SetTurnGunLeft(gunAngle);
        
        // Tembak jika gun sudah dingin dan sudut sudah dalam toleransi
        if (GunHeat == 0 && Math.Abs(GunTurnRemaining) <= angleTolerance) 
        {
            SetFire(bulletPower);
        }
    }
    
    /// Mengecek apakah target sedang bergerak
    private bool IsTargetMoving()
    {
        return primaryTarget.speed != 0 && IsTargetActive();
    }
    
    /// HEAD-ON TARGETING: Tembak langsung ke posisi musuh saat ini
    /// Cocok untuk musuh yang diam atau jarak sangat dekat
    private void ExecuteDirectTargeting(double bulletPower, double angleTolerance)
    {
        SetTurnGunLeft(GunBearingTo(primaryTarget.location.X, primaryTarget.location.Y));
        if (GunHeat == 0 && Math.Abs(GunTurnRemaining) <= angleTolerance) 
        {
            SetFire(bulletPower);
        }
    }
    
    /// Menghitung kecepatan peluru berdasarkan power
    private double CalculateBulletSpeed(double power)
    {
        return CalcBulletSpeed(power);
    }
    
    // ==================== MATH UTILITIES ====================
    /// Mendapatkan jarak ke koordinat tertentu
    private double GetDistanceTo(double targetX, double targetY)
    {
        return DistanceTo(targetX, targetY);
    }
    
    /// Menghitung kuadrat jarak antara dua titik (lebih cepat dari Math.Sqrt)
    private double GetSquaredDistance(PointD a, PointD b)
    {
        double dx = b.X - a.X;
        double dy = b.Y - a.Y;
        return dx * dx + dy * dy;
    }
    
    /// Mengecek apakah ada musuh yang menghalangi jalur lurus antara start dan dest
    private bool IsMovementBlocked(PointD start, PointD end)
    {
        foreach (EnemyData enemy in enemyRegistry.Values) 
        {
            if (!enemy.isAlive) continue;
            // Hitung jarak terdekat dari center musuh ke garis start-dest
            double clearance = GetShortestDistanceToSegment(enemy.location, start, end);
            // Radius bot = 18, tambah margin 4 => threshold 22
            if (clearance < 22) 
            {
                return true; // Path terblokir
            }
        }
        return false; // Path aman
    }
    
    /// Menghitung jarak terpendek dari titik C ke segmen garis AB
    private double GetShortestDistanceToSegment(PointD point, PointD segmentStart, PointD segmentEnd)
    {
        double dx = segmentEnd.X - segmentStart.X;
        double dy = segmentEnd.Y - segmentStart.Y;
        double segmentLengthSq = dx * dx + dy * dy;
        
        if (segmentLengthSq == 0) 
        {
            return Math.Sqrt(GetSquaredDistance(segmentStart, point));
        }
        
        // Proyeksi titik C ke garis AB
        double t = ((point.X - segmentStart.X) * dx + (point.Y - segmentStart.Y) * dy) / segmentLengthSq;
        t = Math.Max(0, Math.Min(1, t)); // Clamp ke segmen
        
        double closestX = segmentStart.X + t * dx;
        double closestY = segmentStart.Y + t * dy;
        
        return Math.Sqrt(GetSquaredDistance(point, new PointD(closestX, closestY)));
    }
    
    // ==================== EVENT HANDLERS ====================
    /// Event handler: saat radar mendeteksi musuh
    public override void OnScannedBot(ScannedBotEvent e)
    {
        // ===== UPDATE DATA MUSUH =====
        UpdateEnemyData(e);
        
        SelectNearestEnemy();
        
        if (primaryTarget.id == e.ScannedBotId && Energy > 5)
        {
            double bulletPower = CalculateOptimalBulletPower();
            double tolerance = GetAngleTolerance(GetDistanceToTarget());
            
            // Jarak sangat dekat (<150): full power dan toleransi besar
            if (GetDistanceToTarget() < 150 && IsTargetActive()) 
            {
                bulletPower = 3;
                tolerance = 5;
            }
            
            // Eksekusi tembakan dengan linear targeting
            ExecuteLinearTargeting(bulletPower, tolerance);
        }
    }
    
    /// Memperbarui data musuh dari event scan
    private void UpdateEnemyData(ScannedBotEvent e)
    {
        EnemyData enemy;
        if (!enemyRegistry.TryGetValue(e.ScannedBotId, out enemy))
        {
            // Musuh baru: tambahkan ke dictionary
            enemy = new EnemyData(e.ScannedBotId, new PointD(e.X, e.Y), e.Energy, e.Speed, e.Direction, e.TurnNumber);
            enemyRegistry.Add(e.ScannedBotId, enemy);
        }
        else
        {
            // Musuh sudah ada: update data terbaru
            enemyRegistry[e.ScannedBotId].location.X = e.X;
            enemyRegistry[e.ScannedBotId].location.Y = e.Y;
            enemyRegistry[e.ScannedBotId].energy = e.Energy;
            enemyRegistry[e.ScannedBotId].speed = e.Speed;
            enemyRegistry[e.ScannedBotId].direction = e.Direction;
            enemyRegistry[e.ScannedBotId].lastSeen = e.TurnNumber;
            enemyRegistry[e.ScannedBotId].isAlive = true;
        }
    }
    
    // Event saat bot musuh mati
    public override void OnBotDeath(BotDeathEvent botDeathEvent)
    {
        if (enemyRegistry.ContainsKey(botDeathEvent.VictimId))
        {
            enemyRegistry[botDeathEvent.VictimId].isAlive = false;
        }
        SelectNearestEnemy();
    }
    
    // Event saat bot menabrak musuh (ramming)
    public override void OnHitBot(HitBotEvent e)
    {
        // Balas dengan tembakan full power
        if (enemyRegistry.ContainsKey(e.VictimId))
        {
            primaryTarget = enemyRegistry[e.VictimId];
        }
        ExecuteDirectTargeting(3, 5);
        
        // Cari titik baru karena posisi sudah berubah
        RectangleD arenaBounds = new RectangleD(45, 45, ArenaWidth - 90, ArenaHeight - 90);
        DetermineNewDestination(arenaBounds);
    }
    
    // Event saat bot terkena tembakan musuh
    public override void OnHitByBullet(HitByBulletEvent bulletHitBotEvent)
    {
        // Jika musuh sangat dekat, balas tembak
        if (IsTargetActive() && GetDistanceToTarget() < 100) 
        {
            ExecuteDirectTargeting(3, 5);
        }
        // Jika terkena tembakan kuat, cari posisi baru
        if (bulletHitBotEvent.Bullet.Power > 1.5 && IsTargetActive()) 
        {
            RectangleD arenaBounds = new RectangleD(45, 45, ArenaWidth - 90, ArenaHeight - 90);
            DetermineNewDestination(arenaBounds);
        }
    }
    
    // Handler untuk wall hit
    // Saat bot menabrak dinding arena
    public override void OnHitWall(HitWallEvent e)
    {
        // Balik arah untuk menghindari wall damage berulang
        SetTurnLeft(180);
        SetForward(100);
    }
    
    // ==================== DATA CLASSES ====================
    /// <summary>
    /// Kelas untuk menyimpan data musuh
    /// </summary>
    public class EnemyData 
    {
        public bool isAlive; // Apakah musuh masih hidup
        public int id;
        public PointD location;
        public double energy, speed, direction;
        public int lastSeen; // Timestamp waktu scan terakhir
    
        public EnemyData(int id, PointD location, double energy, double speed, double direction, int lastSeen) 
        {
            isAlive = true;
            this.id = id;
            this.location = location;
            this.energy = energy;
            this.speed = speed;
            this.direction = direction;
            this.lastSeen = lastSeen;
        }
    }
    
    /// <summary>
    /// Kelas titik dengan koordinat double (presisi lebih tinggi)
    /// </summary>
    public class PointD 
    {
        public double X;
        public double Y;
        
        public PointD(double X, double Y) 
        {
            this.X = X;
            this.Y = Y;
        }
        
        public double DistanceTo(double x, double y) 
        {
            var dx = x - X;
            var dy = y - Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
    
    /// <summary>
    /// Kelas rectangle dengan koordinat double
    /// </summary>
    public class RectangleD
    {
        public double x;
        public double y;
        public double width;
        public double height;
        
        public RectangleD(double x, double y, double width, double height)
        {
            this.x = x;
            this.y = y;
            this.width = width;
            this.height = height;
        }
        
        public bool contains(double px, double py)
        {
            return px >= this.x && px <= this.x + width && py >= this.y && py <= this.y + height;
        }
    }
}