/*
  Warnings:

  - You are about to drop the `players` table. If the table is not empty, all the data it contains will be lost.

*/
-- DropForeignKey
ALTER TABLE "public"."players" DROP CONSTRAINT "players_instance_id_fkey";

-- DropForeignKey
ALTER TABLE "public"."players" DROP CONSTRAINT "players_user_id_fkey";

-- AlterTable
ALTER TABLE "instances" ADD COLUMN     "user_ids" INTEGER[] DEFAULT ARRAY[]::INTEGER[];

-- DropTable
DROP TABLE "public"."players";
