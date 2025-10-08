-- AlterTable
ALTER TABLE "cards" ADD COLUMN     "printed_name" TEXT;

-- AlterTable
ALTER TABLE "faces" ADD COLUMN     "printed_text" TEXT,
ADD COLUMN     "printed_type_line" TEXT;
