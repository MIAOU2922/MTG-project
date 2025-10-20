/*
  Warnings:

  - You are about to drop the `redirects` table. If the table is not empty, all the data it contains will be lost.

*/
-- DropForeignKey
ALTER TABLE "public"."redirects" DROP CONSTRAINT "redirects_user_id_fkey";

-- DropTable
DROP TABLE "public"."redirects";
