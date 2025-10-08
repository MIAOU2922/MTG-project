-- CreateTable
CREATE TABLE "User" (
    "id" INTEGER NOT NULL,
    "created_at" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "last_seen_at" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "User_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "Instance" (
    "id" INTEGER NOT NULL,
    "created_at" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "last_seen_at" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "Instance_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "Player" (
    "instance_id" INTEGER NOT NULL,
    "user_id" INTEGER NOT NULL,

    CONSTRAINT "Player_pkey" PRIMARY KEY ("instance_id","user_id")
);

-- CreateTable
CREATE TABLE "Redirect" (
    "id" INTEGER NOT NULL,
    "user_id" INTEGER NOT NULL,
    "url" TEXT NOT NULL,
    "age" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "Redirect_pkey" PRIMARY KEY ("user_id","id")
);

-- CreateTable
CREATE TABLE "Config" (
    "key" TEXT NOT NULL,
    "value" TEXT NOT NULL,

    CONSTRAINT "Config_pkey" PRIMARY KEY ("key")
);

-- CreateTable
CREATE TABLE "Card" (
    "id" TEXT NOT NULL,
    "oracle_id" TEXT,
    "lang" TEXT,
    "name" TEXT NOT NULL,
    "layout" TEXT,
    "cmc" DOUBLE PRECISION,
    "type_line" TEXT,
    "oracle_text" TEXT,
    "power" TEXT,
    "toughness" TEXT,
    "loyalty" TEXT,
    "defense" TEXT,
    "mana_cost" TEXT,
    "colors" TEXT[],
    "color_identity" TEXT[],
    "rarity" TEXT,
    "artist" TEXT,
    "set" TEXT,
    "set_name" TEXT,
    "collector_number" TEXT,
    "released_at" TIMESTAMP(3),
    "flavor_text" TEXT,
    "image_uris" JSONB,
    "prices" JSONB,
    "legalities" JSONB,
    "related_uris" JSONB,
    "purchase_uris" JSONB,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updatedAt" TIMESTAMP(3) NOT NULL,

    CONSTRAINT "Card_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "Ruling" (
    "id" TEXT NOT NULL,
    "oracle_id" TEXT NOT NULL,
    "source" TEXT,
    "published_at" TIMESTAMP(3),
    "comment" TEXT,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updatedAt" TIMESTAMP(3) NOT NULL,

    CONSTRAINT "Ruling_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "card_faces" (
    "id" SERIAL NOT NULL,
    "cardId" TEXT NOT NULL,
    "name" TEXT,
    "mana_cost" TEXT,
    "type_line" TEXT,
    "oracle_text" TEXT,
    "power" TEXT,
    "toughness" TEXT,
    "loyalty" TEXT,
    "flavor_text" TEXT,
    "artist" TEXT,
    "illustration_id" TEXT,
    "image_uris" JSONB,

    CONSTRAINT "card_faces_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "legalities" (
    "id" SERIAL NOT NULL,
    "format" TEXT NOT NULL,
    "legality" TEXT NOT NULL,
    "cardId" TEXT NOT NULL,

    CONSTRAINT "legalities_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "prices" (
    "cardId" TEXT NOT NULL,
    "usd" TEXT,
    "usd_foil" TEXT,
    "eur" TEXT,
    "eur_foil" TEXT,
    "tix" TEXT
);

-- CreateTable
CREATE TABLE "images" (
    "cardId" TEXT NOT NULL,
    "small" TEXT,
    "normal" TEXT,
    "large" TEXT,
    "png" TEXT,
    "art_crop" TEXT,
    "border_crop" TEXT
);

-- CreateTable
CREATE TABLE "colors" (
    "id" SERIAL NOT NULL,
    "color" TEXT NOT NULL,
    "cardId" TEXT NOT NULL,

    CONSTRAINT "colors_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "color_identities" (
    "id" SERIAL NOT NULL,
    "color" TEXT NOT NULL,
    "cardId" TEXT NOT NULL,

    CONSTRAINT "color_identities_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "keywords" (
    "id" SERIAL NOT NULL,
    "keyword" TEXT NOT NULL,
    "cardId" TEXT NOT NULL,

    CONSTRAINT "keywords_pkey" PRIMARY KEY ("id")
);

-- CreateIndex
CREATE INDEX "Card_oracle_id_idx" ON "Card"("oracle_id");

-- CreateIndex
CREATE INDEX "Card_name_idx" ON "Card"("name");

-- CreateIndex
CREATE INDEX "Card_set_idx" ON "Card"("set");

-- CreateIndex
CREATE INDEX "Ruling_oracle_id_idx" ON "Ruling"("oracle_id");

-- CreateIndex
CREATE INDEX "legalities_format_idx" ON "legalities"("format");

-- CreateIndex
CREATE UNIQUE INDEX "prices_cardId_key" ON "prices"("cardId");

-- CreateIndex
CREATE UNIQUE INDEX "images_cardId_key" ON "images"("cardId");

-- CreateIndex
CREATE INDEX "colors_color_idx" ON "colors"("color");

-- CreateIndex
CREATE INDEX "color_identities_color_idx" ON "color_identities"("color");

-- CreateIndex
CREATE INDEX "keywords_keyword_idx" ON "keywords"("keyword");

-- AddForeignKey
ALTER TABLE "Player" ADD CONSTRAINT "Player_user_id_fkey" FOREIGN KEY ("user_id") REFERENCES "User"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "Player" ADD CONSTRAINT "Player_instance_id_fkey" FOREIGN KEY ("instance_id") REFERENCES "Instance"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "Redirect" ADD CONSTRAINT "Redirect_user_id_fkey" FOREIGN KEY ("user_id") REFERENCES "User"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "card_faces" ADD CONSTRAINT "card_faces_cardId_fkey" FOREIGN KEY ("cardId") REFERENCES "Card"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "legalities" ADD CONSTRAINT "legalities_cardId_fkey" FOREIGN KEY ("cardId") REFERENCES "Card"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "prices" ADD CONSTRAINT "prices_cardId_fkey" FOREIGN KEY ("cardId") REFERENCES "Card"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "images" ADD CONSTRAINT "images_cardId_fkey" FOREIGN KEY ("cardId") REFERENCES "Card"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "colors" ADD CONSTRAINT "colors_cardId_fkey" FOREIGN KEY ("cardId") REFERENCES "Card"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "color_identities" ADD CONSTRAINT "color_identities_cardId_fkey" FOREIGN KEY ("cardId") REFERENCES "Card"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "keywords" ADD CONSTRAINT "keywords_cardId_fkey" FOREIGN KEY ("cardId") REFERENCES "Card"("id") ON DELETE CASCADE ON UPDATE CASCADE;
