-- Phase 2 multi-round support:
-- 1. Link price comparison attachments to RFQ bid rounds
-- 2. Link external RFQ invitations to RFQ bid rounds

ALTER TABLE `price_comparison_attachments`
    ADD COLUMN IF NOT EXISTS `bid_round_id` BIGINT NULL AFTER `rfq_id`;

ALTER TABLE `rfq_external_invitations`
    ADD COLUMN IF NOT EXISTS `bid_round_id` BIGINT NULL AFTER `rfq_id`;

UPDATE `price_comparison_attachments` att
JOIN `rfq_bid_rounds` round1
  ON round1.`rfq_id` = att.`rfq_id`
 AND round1.`round_number` = 1
SET att.`bid_round_id` = round1.`id`
WHERE att.`bid_round_id` IS NULL;

UPDATE `rfq_external_invitations` ext
JOIN `rfq_bid_rounds` round1
  ON round1.`rfq_id` = ext.`rfq_id`
 AND round1.`round_number` = 1
SET ext.`bid_round_id` = round1.`id`
WHERE ext.`bid_round_id` IS NULL;

CREATE INDEX `idx_price_comparison_attachments_rfq_round_line`
    ON `price_comparison_attachments` (`rfq_id`, `bid_round_id`, `line_item_id`);

CREATE INDEX `idx_rfq_external_invitations_rfq_round_email`
    ON `rfq_external_invitations` (`rfq_id`, `bid_round_id`, `email`(191));
