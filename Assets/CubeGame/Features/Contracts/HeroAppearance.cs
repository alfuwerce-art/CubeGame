namespace CubeGame.Features.Contracts
{
    /// <summary>
    /// Appearance chosen in the hero editor (C1). Alpha fields of the creation widget (0x428E40 / 0x42C080):
    /// race <c>+0x18C</c> (0…7), gender <c>+0x194</c> (0 male, 1 female), face <c>+0x198</c> and haircut <c>+0x19C</c>
    /// (index inside the model range of the species), hair colour <c>+0x1A0…0x1A2</c> (RGB bytes).
    /// <see cref="Face"/>/<see cref="Haircut"/> index the ranges of <c>AlphaHeroCreation.HeadModels/HairModels</c>.
    /// </summary>
    public readonly struct HeroAppearance
    {
        public HeroAppearance(int race, int gender, int face, int haircut, byte hairR, byte hairG, byte hairB)
        {
            Race = race;
            Gender = gender;
            Face = face;
            Haircut = haircut;
            HairR = hairR;
            HairG = hairG;
            HairB = hairB;
        }

        public int Race { get; }

        public int Gender { get; }

        public int Face { get; }

        public int Haircut { get; }

        public byte HairR { get; }

        public byte HairG { get; }

        public byte HairB { get; }

        public override string ToString() => $"race={Race} gender={Gender} face={Face} hair={Haircut} rgb=({HairR},{HairG},{HairB})";
    }
}
