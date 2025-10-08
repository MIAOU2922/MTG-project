/*
  Warnings:

  - The primary key for the `legalities` table will be changed. If it partially fails, the table could be left without primary key constraint.
  - You are about to drop the column `cardId` on the `legalities` table. All the data in the column will be lost.
  - You are about to drop the column `id` on the `legalities` table. All the data in the column will be lost.
  - You are about to drop the `Card` table. If the table is not empty, all the data it contains will be lost.
  - You are about to drop the `Config` table. If the table is not empty, all the data it contains will be lost.
  - You are about to drop the `Instance` table. If the table is not empty, all the data it contains will be lost.
  - You are about to drop the `Player` table. If the table is not empty, all the data it contains will be lost.
  - You are about to drop the `Redirect` table. If the table is not empty, all the data it contains will be lost.
  - You are about to drop the `Ruling` table. If the table is not empty, all the data it contains will be lost.
  - You are about to drop the `User` table. If the table is not empty, all the data it contains will be lost.
  - You are about to drop the `card_faces` table. If the table is not empty, all the data it contains will be lost.
  - You are about to drop the `color_identities` table. If the table is not empty, all the data it contains will be lost.
  - You are about to drop the `colors` table. If the table is not empty, all the data it contains will be lost.
  - You are about to drop the `images` table. If the table is not empty, all the data it contains will be lost.
  - You are about to drop the `keywords` table. If the table is not empty, all the data it contains will be lost.
  - You are about to drop the `prices` table. If the table is not empty, all the data it contains will be lost.
  - Added the required column `card_id` to the `legalities` table without a default value. This is not possible if the table is not empty.

*/
-- DropForeignKey
ALTER TABLE "public"."Player" DROP CONSTRAINT "Player_instance_id_fkey";

-- DropForeignKey
ALTER TABLE "public"."Player" DROP CONSTRAINT "Player_user_id_fkey";

-- DropForeignKey
ALTER TABLE "public"."Redirect" DROP CONSTRAINT "Redirect_user_id_fkey";

-- DropForeignKey
ALTER TABLE "public"."card_faces" DROP CONSTRAINT "card_faces_cardId_fkey";

-- DropForeignKey
ALTER TABLE "public"."color_identities" DROP CONSTRAINT "color_identities_cardId_fkey";

-- DropForeignKey
ALTER TABLE "public"."colors" DROP CONSTRAINT "colors_cardId_fkey";

-- DropForeignKey
ALTER TABLE "public"."images" DROP CONSTRAINT "images_cardId_fkey";

-- DropForeignKey
ALTER TABLE "public"."keywords" DROP CONSTRAINT "keywords_cardId_fkey";

-- DropForeignKey
ALTER TABLE "public"."legalities" DROP CONSTRAINT "legalities_cardId_fkey";

-- DropForeignKey
ALTER TABLE "public"."prices" DROP CONSTRAINT "prices_cardId_fkey";

-- DropIndex
DROP INDEX "public"."legalities_format_idx";

-- AlterTable
ALTER TABLE "legalities" DROP CONSTRAINT "legalities_pkey",
DROP COLUMN "cardId",
DROP COLUMN "id",
ADD COLUMN     "card_id" TEXT NOT NULL,
ADD CONSTRAINT "legalities_pkey" PRIMARY KEY ("format", "card_id");

-- DropTable
DROP TABLE "public"."Card";

-- DropTable
DROP TABLE "public"."Config";

-- DropTable
DROP TABLE "public"."Instance";

-- DropTable
DROP TABLE "public"."Player";

-- DropTable
DROP TABLE "public"."Redirect";

-- DropTable
DROP TABLE "public"."Ruling";

-- DropTable
DROP TABLE "public"."User";

-- DropTable
DROP TABLE "public"."card_faces";

-- DropTable
DROP TABLE "public"."color_identities";

-- DropTable
DROP TABLE "public"."colors";

-- DropTable
DROP TABLE "public"."images";

-- DropTable
DROP TABLE "public"."keywords";

-- DropTable
DROP TABLE "public"."prices";

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

    CONSTRAINT "instances_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "players" (
    "instance_id" INTEGER NOT NULL,
    "user_id" INTEGER NOT NULL,

    CONSTRAINT "players_pkey" PRIMARY KEY ("instance_id","user_id")
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
CREATE TABLE "config" (
    "key" TEXT NOT NULL,
    "value" TEXT NOT NULL,
    "created_at" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updated_at" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "config_pkey" PRIMARY KEY ("key")
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
    "set_id" TEXT NOT NULL,
    "collector_number" TEXT NOT NULL,
    "rarity" TEXT NOT NULL,
    "image_url" TEXT NOT NULL,
    "release_at" TIMESTAMP(3) NOT NULL,
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
    "mana_cost" TEXT,
    "power" TEXT,
    "toughness" TEXT,
    "loyalty" TEXT,
    "defense" TEXT,
    "flavor_text" TEXT,
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
CREATE TABLE "Set" (
    "id" TEXT NOT NULL,
    "name" TEXT NOT NULL,
    "type" TEXT NOT NULL,

    CONSTRAINT "Set_pkey" PRIMARY KEY ("id")
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
ALTER TABLE "players" ADD CONSTRAINT "players_user_id_fkey" FOREIGN KEY ("user_id") REFERENCES "users"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "players" ADD CONSTRAINT "players_instance_id_fkey" FOREIGN KEY ("instance_id") REFERENCES "instances"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "redirects" ADD CONSTRAINT "redirects_user_id_fkey" FOREIGN KEY ("user_id") REFERENCES "users"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "cards" ADD CONSTRAINT "cards_set_id_fkey" FOREIGN KEY ("set_id") REFERENCES "Set"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "faces" ADD CONSTRAINT "faces_card_id_fkey" FOREIGN KEY ("card_id") REFERENCES "cards"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "faces" ADD CONSTRAINT "faces_oracle_id_fkey" FOREIGN KEY ("oracle_id") REFERENCES "oracles"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "rulings" ADD CONSTRAINT "rulings_oracle_id_fkey" FOREIGN KEY ("oracle_id") REFERENCES "oracles"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "legalities" ADD CONSTRAINT "legalities_card_id_fkey" FOREIGN KEY ("card_id") REFERENCES "cards"("id") ON DELETE CASCADE ON UPDATE CASCADE;
