-- AlterTable
ALTER TABLE "instances" ADD COLUMN     "card_ids" TEXT[] DEFAULT ARRAY[]::TEXT[];
