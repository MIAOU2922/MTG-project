-- CreateTable
CREATE TABLE "users" (
    "id" INTEGER NOT NULL,
    "created_at" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "last_seen_at" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "users_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "instances" (
    "id" INTEGER NOT NULL,
    "created_at" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "last_seen_at" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "card_ids" TEXT[] DEFAULT ARRAY[]::TEXT[],
    "user_ids" INTEGER[] DEFAULT ARRAY[]::INTEGER[],

    CONSTRAINT "instances_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "redirects" (
    "id" INTEGER NOT NULL,
    "user_id" INTEGER NOT NULL,
    "url" TEXT NOT NULL,
    "age" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "redirects_pkey" PRIMARY KEY ("user_id","id")
);

-- CreateTable
CREATE TABLE "configs" (
    "key" TEXT NOT NULL,
    "value" TEXT NOT NULL,
    "created_at" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updated_at" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "configs_pkey" PRIMARY KEY ("key")
);

-- CreateTable
CREATE TABLE "oracles" (
    "id" TEXT NOT NULL,
    "text" TEXT NOT NULL,

    CONSTRAINT "oracles_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "cards" (
    "id" TEXT NOT NULL,
    "name" TEXT NOT NULL,
    "lang" TEXT NOT NULL,
    "printed_name" TEXT,
    "set_id" TEXT NOT NULL,
    "collector_number" TEXT NOT NULL,
    "rarity" TEXT NOT NULL,
    "image_url" TEXT NOT NULL,
    "release_at" TIMESTAMP(3) NOT NULL,
    "legalities" JSONB NOT NULL DEFAULT '{}',
    "created_at" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updated_at" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "cards_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "faces" (
    "index" INTEGER NOT NULL DEFAULT 0,
    "name" TEXT NOT NULL,
    "oracle_id" TEXT,
    "layout" TEXT NOT NULL,
    "card_id" TEXT NOT NULL,
    "cmc" DOUBLE PRECISION,
    "type_line" TEXT,
    "printed_type_line" TEXT,
    "mana_cost" TEXT,
    "power" TEXT,
    "toughness" TEXT,
    "loyalty" TEXT,
    "defense" TEXT,
    "flavor_text" TEXT,
    "printed_text" TEXT,
    "keywords" TEXT[],
    "color_identities" TEXT[],
    "colors" TEXT[],
    "flavor_name" TEXT,
    "image_url" TEXT NOT NULL,
    "created_at" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updated_at" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "faces_pkey" PRIMARY KEY ("card_id","index")
);

-- CreateTable
CREATE TABLE "sets" (
    "id" TEXT NOT NULL,
    "set" TEXT,
    "name" TEXT NOT NULL,
    "type" TEXT NOT NULL,
    "created_at" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updated_at" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "sets_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "rulings" (
    "id" TEXT NOT NULL,
    "oracle_id" TEXT NOT NULL,
    "published_at" TIMESTAMP(3) NOT NULL,
    "comment" TEXT,
    "created_at" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updated_at" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "rulings_pkey" PRIMARY KEY ("id")
);

-- CreateIndex
CREATE UNIQUE INDEX "cards_lang_set_id_collector_number_key" ON "cards"("lang", "set_id", "collector_number");

-- AddForeignKey
ALTER TABLE "redirects" ADD CONSTRAINT "redirects_user_id_fkey" FOREIGN KEY ("user_id") REFERENCES "users"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "cards" ADD CONSTRAINT "cards_set_id_fkey" FOREIGN KEY ("set_id") REFERENCES "sets"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "faces" ADD CONSTRAINT "faces_card_id_fkey" FOREIGN KEY ("card_id") REFERENCES "cards"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "faces" ADD CONSTRAINT "faces_oracle_id_fkey" FOREIGN KEY ("oracle_id") REFERENCES "oracles"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "rulings" ADD CONSTRAINT "rulings_oracle_id_fkey" FOREIGN KEY ("oracle_id") REFERENCES "oracles"("id") ON DELETE CASCADE ON UPDATE CASCADE;
