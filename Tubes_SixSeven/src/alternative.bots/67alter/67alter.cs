using System;
using System.Drawing;
using Robocode.TankRoyale.BotApi;
using Robocode.TankRoyale.BotApi.Events;


public class Bot67alter : Bot
{
    // ── Zona tengah arena (bukan tepi/pojok seperti sample) ──
    // Bot selalu berusaha berada di 30-70% lebar/tinggi arena
    private const double ZoneMargin = 0.30;

    // ── Jarak tembak maksimal ─────────────────────────────────
    private const double MaxShootDist = 500;

    // ── Data musuh ───────────────────────────────────────────
    double _ex = -1, _ey = -1;
    double _eDir = 0, _eSpd = 0;
    double _eEnergy     = 100.0;
    double _ePrevEnergy = 100.0;
    bool   _found = false;
    int    _age   = 0;

    // ── Zigzag state ─────────────────────────────────────────
    int    _zigDir    = 1;      // arah zigzag: +1 atau -1
    int    _zigTick   = 0;      // counter ganti arah
    int    _zigPeriod = 15;     // seberapa sering ganti arah (acak)
    int    _dodge     = 0;      // cooldown dodge
    Random _rng       = new Random();

    // ── Entry point ──────────────────────────────────────────
    static void Main(string[] args) => new Bot67alter().Start();
    Bot67alter() : base(BotInfo.FromFile("67alter.json")) { }

    // ════════════════════════════════════════════════════════
    //  RUN
    // ════════════════════════════════════════════════════════
    public override void Run()
    {
        // Warna merah gelap — identitas visual berbeda
        BodyColor   = Color.DarkRed;
        TurretColor = Color.Black;
        RadarColor  = Color.Orange;
        BulletColor = Color.Red;
        ScanColor   = Color.Gray;
        TracksColor = Color.DarkGray;
        GunColor    = Color.Maroon;

        // Gun & radar independen dari body
        AdjustGunForBodyTurn   = true;
        AdjustRadarForBodyTurn = true;
        AdjustRadarForGunTurn  = true;

        while (IsRunning)
        {
            _age++;
            if (_dodge > 0) _dodge--;

            // 1. Radar sweep — tidak lock 1 musuh (beda dari TrackFire)
            DoRadar();

            // 2. Zone control — tetap di tengah arena
            // (beda dari Corners/Walls yang mepet tepi)
            DoZoneControl();

            // 3. Zigzag dinamis — gerakan tidak terprediksi
            DoZigzag();

            // 4. Tembak dengan greedy firepower
            DoFire();
        }
    }

    // ════════════════════════════════════════════════════════
    //  RADAR — sweep lebar, bukan lock 1 target
    //  Beda dari TrackFire yang lock radar ke 1 musuh terus
    //  Di sini radar terus sweep untuk mendeteksi semua musuh
    // ════════════════════════════════════════════════════════
    void DoRadar()
    {
        if (!_found || _age > 6)
        {
            // Sweep penuh sampai ketemu musuh
            TurnRadarRight(360);
        }
        else
        {
            // Narrow lock + overshoot kecil agar tidak kehilangan target
            double angle  = DirectionTo(_ex, _ey);
            double offset = NormalizeRelativeAngle(angle - RadarDirection);
            // Overshoot 20 derajat (lebih lebar dari lock biasa)
            // agar juga bisa deteksi musuh lain di sekitar target
            double sweep  = offset + (offset >= 0 ? 20 : -20);
            TurnRadarRight(sweep);
        }
    }

    // ════════════════════════════════════════════════════════
    //  ZONE CONTROL — tetap di zona tengah arena (30-70%)
    //  Ini KEBALIKAN dari Corners (pojok) dan Walls (tepi)
    //  Tengah arena = punya opsi kabur ke semua arah
    // ════════════════════════════════════════════════════════
    void DoZoneControl()
    {
        double zoneLeft   = ArenaWidth  * ZoneMargin;
        double zoneRight  = ArenaWidth  * (1 - ZoneMargin);
        double zoneBottom = ArenaHeight * ZoneMargin;
        double zoneTop    = ArenaHeight * (1 - ZoneMargin);

        bool outOfZone = X < zoneLeft || X > zoneRight
                      || Y < zoneBottom || Y > zoneTop;

        if (outOfZone)
        {
            // Kembali ke tengah arena
            double cx = ArenaWidth  / 2.0;
            double cy = ArenaHeight / 2.0;
            double b  = NormalizeRelativeAngle(DirectionTo(cx, cy) - Direction);
            TurnRight(b);
            MaxSpeed = 8;
            Forward(100);
        }
    }

    // ════════════════════════════════════════════════════════
    //  ZIGZAG DINAMIS — gerakan tidak terprediksi
    //  Bukan maju-mundur lurus (MyFirstBot) bukan tepi (Walls)
    //  Zigzag di tengah arena dengan periode acak
    //  → sangat susah kena prediktif targeting musuh
    // ════════════════════════════════════════════════════════
    void DoZigzag()
    {
        if (!_found || _age > 8)
        {
            // Tidak ada musuh → jelajah tengah arena
            TurnRight(15);
            MaxSpeed = 6;
            Forward(60);
            return;
        }

        double dist    = DistanceTo(_ex, _ey);
        double toEnemy = DirectionTo(_ex, _ey);

        // Bergerak tegak lurus musuh + sudut zigzag (30-60 derajat)
        // Bukan circular strafe biasa, tapi ada komponen maju-mundur
        double zigAngle = toEnemy + (75 * _zigDir);
        double bearing  = NormalizeRelativeAngle(zigAngle - Direction);
        TurnRight(bearing);

        // Speed adaptif
        MaxSpeed = (_dodge > 0) ? 8 : 6;

        // Jaga jarak optimal 200-350 px dari musuh
        if (dist > 380)
            Forward(80);      // terlalu jauh → mendekat
        else if (dist < 180)
            Back(70);         // terlalu dekat → menjauh
        else
            Forward(55);      // zona ideal → zigzag

        // Ganti arah zigzag dengan periode semi-acak
        _zigTick++;
        if (_zigTick >= _zigPeriod)
        {
            _zigTick   = 0;
            _zigDir   *= -1;
            // Periode acak 10-25 turn → tidak terprediksi
            _zigPeriod = 10 + _rng.Next(15);
        }
    }

    // ════════════════════════════════════════════════════════
    //  GREEDY FIREPOWER — pilih power tembak paling optimal
    //  Lebih canggih dari sample Fire (selalu Fire(1))
    //  Mempertimbangkan: jarak, energi kita, energi musuh
    // ════════════════════════════════════════════════════════
    void DoFire()
    {
        if (!_found || _age > 6 || GunHeat > 0) return;

        double dist = DistanceTo(_ex, _ey);
        double en   = Energy;

        // Tidak tembak kalau musuh terlalu jauh
        if (dist > MaxShootDist) return;

        // Tidak tembak kalau energi kritis
        if (en < 8) return;

        // ── Greedy: pilih firepower paling optimal saat ini ──
        double power;
        if (_eEnergy < 10)
            power = Math.Min(3.0, en - 0.5); // kill shot
        else if (en < 20)
            power = 0.5;                     // hemat energi
        else if (dist <= 100)
            power = 3.0;
        else if (dist <= 200)
            power = 2.5;
        else if (dist <= 300)
            power = 2.0;
        else if (dist <= 400)
            power = 1.5;
        else
            power = 1.0;

        if (en < power) return;

        // ── Predictive targeting ──
        double bSpeed = 20 - 3 * power;
        double tTime  = dist / bSpeed;
        double predX  = _ex + Math.Cos(_eDir * Math.PI / 180) * _eSpd * tTime;
        double predY  = _ey + Math.Sin(_eDir * Math.PI / 180) * _eSpd * tTime;

        // Clamp dalam arena
        predX = Math.Max(30, Math.Min(ArenaWidth  - 30, predX));
        predY = Math.Max(30, Math.Min(ArenaHeight - 30, predY));

        // Arahkan gun ke prediksi posisi
        double aim    = DirectionTo(predX, predY);
        double offset = NormalizeRelativeAngle(aim - GunDirection);
        TurnGunRight(offset);

        // Tembak hanya kalau sudah cukup terarah (< 10 derajat)
        if (Math.Abs(NormalizeRelativeAngle(aim - GunDirection)) < 10)
            Fire(power);
    }

    // ════════════════════════════════════════════════════════
    //  EVENT HANDLERS
    // ════════════════════════════════════════════════════════

    public override void OnScannedBot(ScannedBotEvent e)
    {
        _ePrevEnergy = _eEnergy;
        _found   = true;
        _ex      = e.X;
        _ey      = e.Y;
        _eDir    = e.Direction;
        _eSpd    = e.Speed;
        _eEnergy = e.Energy;
        _age     = 0;

        // ── Deteksi musuh menembak (energy drop 0.1–3.0) ──
        // Ini greedy dodge: hindari SEBELUM peluru sampai
        double drop = _ePrevEnergy - e.Energy;
        if (drop >= 0.1 && drop <= 3.0 && _dodge <= 0)
        {
            _zigDir   *= -1;          // balik arah zigzag
            _zigTick   = 0;
            _zigPeriod = 10 + _rng.Next(8);
            _dodge     = 12;
            MaxSpeed   = 8;
            Forward(90 * _zigDir);
        }
    }

    public override void OnHitByBullet(HitByBulletEvent e)
    {
        // Dodge tegak lurus arah datang peluru
        double perp = NormalizeRelativeAngle(e.Bullet.Direction + 90 - Direction);
        TurnRight(perp);
        MaxSpeed = 8;
        Forward(110);
        _zigDir  *= -1;
        _zigTick  = 0;
        _dodge    = 10;
    }

    public override void OnHitWall(HitWallEvent e)
    {
        // Balik ke tengah arena (bukan telusuri dinding seperti Walls)
        double cx = ArenaWidth  / 2.0;
        double cy = ArenaHeight / 2.0;
        double b  = NormalizeRelativeAngle(DirectionTo(cx, cy) - Direction);
        TurnRight(b);
        MaxSpeed = 8;
        Forward(120);
        _zigDir *= -1;
    }

    public override void OnHitBot(HitBotEvent e)
    {
        // Kalau lebih kuat → serang, kalau lebih lemah → kabur
        if (Energy > _eEnergy && Energy > 25)
        {
            Fire(Math.Min(3.0, Energy - 1));
            Forward(40);
        }
        else
        {
            Back(80);
            TurnRight(45 * _zigDir);
        }
    }

    public override void OnBotDeath(BotDeathEvent e)
    {
        _found = false;
        _age   = 99;
    }
}