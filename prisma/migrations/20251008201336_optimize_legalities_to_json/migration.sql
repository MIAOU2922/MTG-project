/*
  Warnings:

  - You are about to drop the `legalities` table. If the table is not empty, all the data it contains will be lost.

*/
-- DropForeignKey
ALTER TABLE "public"."legalities" DROP CONSTRAINT "legalities_card_id_fkey";

-- AlterTable
ALTER TABLE "cards" ADD COLUMN     "legalities" JSONB NOT NULL DEFAULT '{}';

-- AlterTable
ALTER TABLE "sets" ADD COLUMN     "set" TEXT;

-- DropTable
DROP TABLE "public"."legalities";
