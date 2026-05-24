using System;
using System.Collections.Generic;
using System.Drawing;
using Robocode.TankRoyale.BotApi;
using Robocode.TankRoyale.BotApi.Events;

public class SixSeven : Bot
{   
    // ==================== KONSTANTA DAN VARIABEL GLOBAL ====================
    private const double DegToRadConst = Math.PI / 180; // Konstanta untuk mengubah derajat menjadi radians
    private Dictionary<int, Enemy> enemies; // Dictionary untuk menyimpan data musuh saat scan
    private Enemy target; // Target saat ini
    private PointD curPos, nextPos; // Posisi saat ini dan tujuan berikutnya
    
    // Konfigurasi kekuatan tembakan
    private double MIN_POWER = 0.5;
    private double MAX_POWER = 3.0;
    private double BASE_DISTANCE = 150.0;
    
    // Counter untuk metode gerak stop and go
    private int stopAndGoCounter = 0;
    private int currentInterval = 0;
    
    // Interval gerak dan diam saat 1v1
    private const int MinMoveInterval = 30;
    private const int MaxMoveInterval = 180;
    private const int MinStopInterval = 5;
    private const int MaxStopInterval = 10;
    private bool isMoving = true;
    private static Random rand = new Random();
    
    static void Main(string[] args)
    {
        new SixSeven().Start();
    }

    public SixSeven() : base(BotInfo.FromFile("SixSeven.json")) { }

    public override void Run()
    {
        // ==================== KONFIGURASI BOT ====================
        // Memastikan radar dan gun independen dari tubuh bot
        AdjustGunForBodyTurn = true;
        AdjustRadarForGunTurn = true;
        AdjustRadarForBodyTurn = true;
        
        // Warna-warna bot (theme pink/magenta khas SixSeven)
        BodyColor = Color.FromArgb(255, 209, 220);
        TurretColor = Color.FromArgb(255, 182, 193);
        RadarColor = Color.FromArgb(255, 240, 245);
        BulletColor = Color.FromArgb(255, 20, 147);
        ScanColor = Color.FromArgb(255, 228, 236);
        TracksColor = Color.FromArgb(199, 120, 150);
        GunColor = Color.FromArgb(255, 192, 203);

        // ==================== INISIALISASI DATA ====================
        target = new Enemy(0, new PointD(X, Y), 0, 0, 0, 0);
        target.active = false;
        enemies = new Dictionary<int, Enemy>();
        curPos = new PointD(X, Y);
        nextPos = curPos;
        
        // Batasan arena (diberi margin 45 unit dari tepi)
        RectangleD battlefield = new RectangleD(45, 45, ArenaWidth - 90, ArenaHeight - 90);
        
        // Inisialisasi interval random untuk stop-and-go movement
        currentInterval = rand.Next(MinMoveInterval, MaxMoveInterval + 1);
        
        // ==================== MAIN LOOP ====================
        while (IsRunning)
        {
            // === RADAR: Memutar radar terus menerus agar tidak kehilangan jejak musuh ===
            SetTurnRadarLeft(60);
            
            // === RADAR: Cari musuh yang paling lama tidak discan ===
            try {
                int stalestTime = int.MaxValue;
                foreach (Enemy en in enemies.Values) {
                    if (TurnNumber > 20 && en.active && en.scanTime < stalestTime) {
                        stalestTime = en.scanTime;
                        SetTurnRadarLeft(Math.Sign(RadarBearingTo(en.location.X, en.location.Y)) * 60);
                    }
                }
            } catch (NullReferenceException e) { }

            // === TARGET SELECTION: Cari target terdekat ===
            FindNewTarget();
            
            // Update posisi saat ini
            curPos.X = X;
            curPos.Y = Y;
            
            // === MOVEMENT: Bergerak setelah setidaknya data semua musuh telah didapatkan ===
            if (TurnNumber > 9 && target.active) {
                Move(battlefield);
            }
            
            // Eksekusi semua perintah movement
            Go();
        }
    }
    
    // ==================== MOVEMENT STRATEGY ====================
    private void Move(RectangleD battlefield) 
    {
        bool isOneVsOne = EnemyCount == 1; // Mengecek apakah mode 1 vs 1

        // ===== STRATEGI STOP-AND-GO (khusus 1vs1) =====
        // Tujuan: mengecoh musuh yang menggunakan linear targeting
        if (isOneVsOne && DistanceTo(target.location.X, target.location.Y) > 200) 
        {
            if (stopAndGoCounter >= currentInterval) 
            {
                isMoving = !isMoving;
                stopAndGoCounter = 0;

                if (isMoving) 
                {
                    currentInterval = rand.Next(MinMoveInterval, MaxMoveInterval + 1);
                } 
                else 
                {
                    currentInterval = rand.Next(MinStopInterval, MaxStopInterval + 1);
                }
            }

            if (isMoving) 
            {
                double distanceToDest = DistanceTo(nextPos.X, nextPos.Y);
                if (distanceToDest < 15) 
                {
                    GenerateWaypointSixSeven(DistanceTo(target.location.X, target.location.Y), battlefield);
                } 
                else 
                {
                    double angle = BearingTo(nextPos.X, nextPos.Y);
                    double direction = 1;
                    
                    if (Math.Cos(DegToRadConst * angle) < 0) 
                    {
                        angle -= 180;
                        direction = -1;
                    }

                    SetTurnLeft(angle);
                    SetForward(distanceToDest * direction);
                    TargetSpeed = Math.Abs(angle) > 60 ? 0 : 8;
                }
            } 
            else 
            {
                SetForward(0);
                TargetSpeed = 0;
            }

            stopAndGoCounter++;
        } 
        else 
        {
            // ===== NAVIGASI STANDAR (untuk FFA atau jarak dekat) =====
            double distanceToDest = DistanceTo(nextPos.X, nextPos.Y);

            // Mencari titik destinasi baru jika sudah sampai
            if (distanceToDest < 15) 
            {
                GenerateWaypointSixSeven(DistanceTo(target.location.X, target.location.Y), battlefield);
            } 
            else 
            {
                // Bergerak ke arah tujuan
                double angle = BearingTo(nextPos.X, nextPos.Y);
                double direction = 1;
                
                // Jika sudut > 90 derajat, lebih efisien mundur
                if (Math.Cos(DegToRadConst * angle) < 0) 
                {
                    angle -= 180;
                    direction = -1;
                }

                SetTurnLeft(angle);
                SetForward(distanceToDest * direction);
                TargetSpeed = Math.Abs(angle) > 60 ? 0 : 8;
            }
        }
    }

    // ==================== WAYPOINT SELECTION SIXSEVEN ====================
    // GREEDY SELECTION: Mencari titik tujuan terbaik di sekitar bot
    // Himpunan kandidat: 360 titik dengan radius dan sudut bervariasi
    // Fungsi seleksi: pilih titik dengan nilai RiskFunction terendah
    private void GenerateWaypointSixSeven(double distanceToTarget, RectangleD battlefield) 
    {
        PointD test;
        double risk = double.PositiveInfinity;
        double currentRisk;

        int i = 0;
        do {
            test = CalcPoint(curPos, Math.Min(0.7 * distanceToTarget, 100 + 150 * rand.NextDouble()), 2 * Math.PI * rand.NextDouble());
            currentRisk = RiskFunctionSixSeven(test);
            if (battlefield.contains(test.X, test.Y) && currentRisk < risk && !IsPathBlockedSixSeven(curPos, test)) {
                risk = currentRisk;
                nextPos = test;
            }
            i++;
        } while (i < 360);
    }

    // FUNGSI OBJEKTIF SIXSEVEN: Menghitung tingkat risiko suatu titik tujuan
    // Semakin tinggi nilai = semakin berbahaya (banyak musuh di sekitar)
    private double RiskFunctionSixSeven(PointD dest) 
    {
        double risk = 0.08 / distanceSquared(dest, curPos);
        
        foreach (var enemy in enemies.Values) 
        {
            if (!enemy.active) continue;
            
            double energyRatio = Math.Min(enemy.energy / Energy, 2);
            double perpendicularity = Math.Abs(Math.Cos(CalcAngleP(curPos, dest) - CalcAngleP(enemy.location, dest)));
            double distanceFactor = distanceSquared(dest, enemy.location);
            
            risk += energyRatio * (1 + perpendicularity) / distanceFactor;
        }
        return risk;
    }

    // ==================== TARGET SELECTION ====================
    // GREEDY SELECTION: Memilih musuh dengan jarak terdekat sebagai target
    private void FindNewTarget()
    {
        double minDistance = double.MaxValue;

        foreach (var enemy in enemies.Values)
        {
            if (!enemy.active) continue;

            double distance = DistanceTo(enemy.location.X, enemy.location.Y);
            if (distance < minDistance)
            {
                minDistance = distance;
                target = enemy;
            }
        }
    }

    // ==================== EVENT HANDLERS ====================
    public override void OnScannedBot(ScannedBotEvent e)
    {
        // Memasukkan atau memperbarui data musuh ke dalam dictionary
        Enemy en;
        if (!enemies.TryGetValue(e.ScannedBotId, out en)) 
        {
            // Musuh baru: tambahkan ke dictionary
            en = new Enemy(e.ScannedBotId, new PointD(e.X, e.Y), e.Energy, e.Speed, e.Direction, e.TurnNumber);
            enemies.Add(e.ScannedBotId, en);
        } 
        else 
        {
            // Update data musuh yang sudah ada
            enemies[e.ScannedBotId].location.X = e.X;
            enemies[e.ScannedBotId].location.Y = e.Y;
            enemies[e.ScannedBotId].energy = e.Energy;
            enemies[e.ScannedBotId].speed = e.Speed;
            enemies[e.ScannedBotId].direction = e.Direction;
            enemies[e.ScannedBotId].scanTime = TurnNumber;
            enemies[e.ScannedBotId].active = true;
        }
        
        FindNewTarget(); // Cari target terdekat
        
        // Radar lock ke target utama
        if (target.id == e.ScannedBotId) 
        {
            double radarBearing = RadarBearingTo(target.location.X, target.location.Y);
            SetTurnRadarLeft(radarBearing + Math.Sign(radarBearing) * 15);
        }
        
        // ===== LOGIC TEMBAKAN =====
        if (target.id == e.ScannedBotId) 
        {
            // Menentukan power dan toleransi angle dari tembakan
            double safeDistance = Math.Max(DistanceTo(target.location.X, target.location.Y), 1.0);
            double distanceComponent = 0.65 + (BASE_DISTANCE / safeDistance);
            double enemyEnergyLimit = target.energy * 0.3;
            double selfEnergyLimit = Energy * 0.2;  
            double bulletPower = Math.Min(Math.Min(enemyEnergyLimit, selfEnergyLimit), distanceComponent);
            bulletPower = Math.Min(MAX_POWER, Math.Max(MIN_POWER, bulletPower));
            
            // Menghemat energi ketika energi rendah
            if (Energy < 10) 
            { 
                bulletPower = MIN_POWER;
            }
            
            // Hitung angle tolerance berdasarkan jarak
            double maxAngleTolerance = 5.0;
            double minDistanceForMaxTolerance = 50.0;
            double maxDistanceForZeroTolerance = 400.0;
            double angleTolerance;
            
            if (safeDistance <= minDistanceForMaxTolerance)
            {
                angleTolerance = maxAngleTolerance;
            }
            else if (safeDistance <= maxDistanceForZeroTolerance)
            {
                double scale = (maxDistanceForZeroTolerance - safeDistance) / (maxDistanceForZeroTolerance - minDistanceForMaxTolerance);
                angleTolerance = maxAngleTolerance * scale;
            }
            else
            {
                angleTolerance = 2.0;
            }
            
            // Jarak sangat dekat (<150): full power dan toleransi besar
            if (DistanceTo(target.location.X, target.location.Y) < 150 && target.active) 
            {
                bulletPower = 3;
                angleTolerance = 5;
            }
            
            // Stop menembak saat energi sedikit (prioritas survival)
            if (Energy > 1) 
            { 
                LinearTargetingSixSeven(bulletPower, angleTolerance);
            }
        }
    }

    // ==================== TARGETING SYSTEM SIXSEVEN ====================
    // LINEAR TARGETING SIXSEVEN: Memprediksi posisi musuh berdasarkan kecepatan konstan
    // Menyelesaikan persamaan kuadrat untuk mencari waktu tempuh peluru
    private void LinearTargetingSixSeven(double bulletPower, double angleTolerance)
    {
        // Jika musuh diam, langsung tembak posisinya saat ini
        if (target.speed == 0) 
        { 
            HeadOnTargetingSixSeven(bulletPower, angleTolerance);
            return;
        } 

        // Ekstrapolasi posisi musuh dengan asumsi kecepatan linear
        double distance = DistanceTo(target.location.X, target.location.Y);
        double bulletSpeed = CalcBulletSpeed(bulletPower);
        
        // Vektor kecepatan musuh
        double enemyVX = target.speed * Math.Cos(target.direction * Math.PI / 180);
        double enemyVY = target.speed * Math.Sin(target.direction * Math.PI / 180);
        
        // Vektor posisi relatif
        double dx = target.location.X - X;
        double dy = target.location.Y - Y;
        
        // Persamaan kuadrat: |P + V*t| = bulletSpeed * t
        double a = enemyVX * enemyVX + enemyVY * enemyVY - bulletSpeed * bulletSpeed;
        double b = 2 * (dx * enemyVX + dy * enemyVY);
        double c = dx * dx + dy * dy;
        double discriminant = b * b - 4 * a * c;

        double t = 0;
        if (a != 0 && discriminant >= 0)
        {
            double t1 = (-b + Math.Sqrt(discriminant)) / (2 * a);
            double t2 = (-b - Math.Sqrt(discriminant)) / (2 * a);
            t = (t1 > 0) ? t1 : (t2 > 0) ? t2 : 0;
        }
        else
        {
            t = distance / bulletSpeed;
        }
        
        // Prediksi posisi musuh setelah waktu t
        double enemyXPredicted = target.location.X + enemyVX * t;
        double enemyYPredicted = target.location.Y + enemyVY * t;
        
        // Clamp ke batas arena agar tidak keluar
        enemyXPredicted = Math.Max(0, Math.Min(enemyXPredicted, ArenaWidth));
        enemyYPredicted = Math.Max(0, Math.Min(enemyYPredicted, ArenaHeight));
        
        // Arahkan gun ke posisi prediksi
        double angle = GunBearingTo(enemyXPredicted, enemyYPredicted);
        SetTurnGunLeft(angle);
        
        // Tembak jika gun sudah dingin dan sudut sudah dalam toleransi
        if (GunHeat == 0 && Math.Abs(GunTurnRemaining) <= angleTolerance) 
        {
            SetFire(bulletPower);
        }
    }

    // HEAD-ON TARGETING SIXSEVEN: Tembak langsung ke posisi musuh saat ini
    // Cocok untuk musuh yang diam atau jarak sangat dekat
    public void HeadOnTargetingSixSeven(double bulletPower, double angleTolerance) 
    {
        SetTurnGunLeft(GunBearingTo(target.location.X, target.location.Y));
        if (GunHeat == 0 && Math.Abs(GunTurnRemaining) <= angleTolerance) 
        {
            SetFire(bulletPower);
        }
    }
    
    // ==================== EVENT HANDLERS LAINNYA ====================
    public override void OnBotDeath(BotDeathEvent botDeathEvent)
    {
        // Menonaktifkan boolean active di musuh yang mati
        if (enemies.ContainsKey(botDeathEvent.VictimId))
        {
            enemies[botDeathEvent.VictimId].active = false;
        }
        FindNewTarget(); // Cari target baru
    }

    public override void OnHitBot(HitBotEvent e)
    {
        // Saat menabrak musuh, jadikan dia target dan balas tembak
        if (enemies.ContainsKey(e.VictimId))
        {
            target = enemies[e.VictimId];
        }
        HeadOnTargetingSixSeven(3, 5); // Balas dengan tembakan full power
        
        // Cari titik baru karena posisi sudah berubah
        RectangleD battlefield = new RectangleD(45, 45, ArenaWidth - 90, ArenaHeight - 90);
        GenerateWaypointSixSeven(double.MaxValue, battlefield);
    }

    public override void OnHitByBullet(HitByBulletEvent bulletHitBotEvent)
    {
        // Jika musuh sangat dekat, balas tembak dengan full power
        if (DistanceTo(target.location.X, target.location.Y) < 100) 
        {
            HeadOnTargetingSixSeven(3, 5);
        }
        
        // Jika terkena tembakan kuat (>1.5 power), cari posisi baru untuk menghindar
        if (bulletHitBotEvent.Bullet.Power > 1.5) 
        {
            RectangleD battlefield = new RectangleD(45, 45, ArenaWidth - 90, ArenaHeight - 90);
            GenerateWaypointSixSeven(double.MaxValue, battlefield);
        }
    }
    
    // Handler untuk wall hit - saat bot menabrak dinding arena
    public override void OnHitWall(HitWallEvent e)
    {
        // Arahkan ke tengah arena untuk menghindari wall stuck
        double centerX = ArenaWidth / 2.0;
        double centerY = ArenaHeight / 2.0;
        double bearingToCenter = NormalizeRelativeAngle(BearingTo(centerX, centerY));
        
        // Putar badan ke arah tengah arena
        SetTurnLeft(bearingToCenter);
        
        // Mundur sedikit lalu maju
        SetBack(80);
        TargetSpeed = MaxSpeed * 0.8;
    }

    // ==================== MATH UTILITIES ====================
    // Menghitung sudut vektor antara dua titik (dalam radian)
    private double CalcAngleP(PointD p1, PointD p2) 
    {
        return Math.Atan2(p2.Y - p1.Y, p2.X - p1.X);
    }

    // Menghitung titik baru dari titik awal, jarak, dan sudut
    private PointD CalcPoint(PointD p, double dest, double angle) 
    {
        return new PointD(p.X + dest * Math.Cos(angle), p.Y + dest * Math.Sin(angle));
    }

    // Menghitung kuadrat jarak antara dua titik (lebih cepat dari Math.Sqrt)
    private double distanceSquared(PointD p1, PointD p2) 
    {
        double dx = p2.X - p1.X;
        double dy = p2.Y - p1.Y;
        return dx * dx + dy * dy;
    }

    // Mengecek apakah ada musuh yang menghalangi jalur lurus antara start dan dest
    private bool IsPathBlockedSixSeven(PointD start, PointD dest) 
    {
        foreach (var enemy in enemies.Values) 
        {
            if (!enemy.active) continue;
            
            double enemyDist = DistanceToLineSegmentSixSeven(enemy.location, start, dest);
            
            // Radius bot = 18, threshold 36 untuk aman (2x radius)
            if (enemyDist < 36) 
            {
                return true; // Path terblokir
            }
        }
        return false; // Path aman
    }

    // Menghitung jarak terpendek dari titik C ke segmen garis AB
    private double DistanceToLineSegmentSixSeven(PointD C, PointD A, PointD B) 
    {
        double dx = B.X - A.X;
        double dy = B.Y - A.Y;
        double lengthSquared = dx * dx + dy * dy;
        
        if (lengthSquared == 0) 
        {
            return Math.Sqrt(distanceSquared(A, C));
        }

        double t = ((C.X - A.X) * dx + (C.Y - A.Y) * dy) / lengthSquared;
        t = Math.Max(0, Math.Min(1, t));

        double closestX = A.X + t * dx;
        double closestY = A.Y + t * dy;
        return Math.Sqrt(distanceSquared(C, new PointD(closestX, closestY)));
    }

    // ==================== DATA CLASSES ====================
    /// <summary>
    /// Kelas untuk menyimpan data musuh
    /// </summary>
    public class Enemy 
    {
        public bool active; // Menentukan apakah musuh masih aktif atau tidak
        public int id; // ID musuh
        public PointD location; // Lokasi musuh (x, y)
        public double energy; // Energi musuh
        public double speed; // Kecepatan musuh
        public double direction; // Arah musuh (derajat)
        public int scanTime; // Timestamp waktu scan terakhir
    
        // Constructor
        public Enemy(int id, PointD location, double energy, double speed, double direction, int scanTime) 
        {
            active = true;
            this.id = id;
            this.location = location;
            this.energy = energy;
            this.speed = speed;
            this.direction = direction;
            this.scanTime = scanTime;
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
    /// Kelas rectangle dengan koordinat double untuk batasan arena
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