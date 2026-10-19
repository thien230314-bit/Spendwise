import React, { useRef, useState } from 'react';
import { UploadCloud, FileText, Image as ImageIcon, X, AlertCircle } from 'lucide-react';
import { AttachmentMetadata } from '../../types';
import { useI18n } from '../../i18n/I18nContext';

interface AttachmentUploaderProps {
  attachments: AttachmentMetadata[];
  onChange: (attachments: AttachmentMetadata[]) => void;
  maxFiles?: number;
  maxSizeMb?: number;
  disabled?: boolean;
}

export const AttachmentUploader: React.FC<AttachmentUploaderProps> = ({
  attachments,
  onChange,
  maxFiles = 5,
  maxSizeMb = 5,
  disabled = false,
}) => {
  const { locale } = useI18n();
  const isVi = locale === 'vi-VN';
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [isDragging, setIsDragging] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const allowedTypes = [
    'image/jpeg',
    'image/jpg',
    'image/png',
    'image/webp',
    'application/pdf',
  ];

  const formatFileSize = (bytes: number): string => {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  };

  const processFiles = (files: FileList | null) => {
    if (!files || files.length === 0 || disabled) return;
    setErrorMessage(null);

    const remainingSlots = maxFiles - attachments.length;
    if (remainingSlots <= 0) {
      setErrorMessage(
        isVi
          ? `Bạn chỉ có thể đính kèm tối đa ${maxFiles} tệp tin.`
          : `You can only attach up to ${maxFiles} files.`
      );
      return;
    }

    const fileList = Array.from(files).slice(0, remainingSlots);

    fileList.forEach((file) => {
      // Validate type
      if (!allowedTypes.includes(file.type.toLowerCase())) {
        setErrorMessage(
          isVi
            ? `Định dạng "${file.name}" không được hỗ trợ. Vui lòng chọn ảnh JPG, PNG, WEBP hoặc tệp PDF.`
            : `File format of "${file.name}" is not supported. Please select JPG, PNG, WEBP images or PDF files.`
        );
        return;
      }

      // Validate size
      const maxBytes = maxSizeMb * 1024 * 1024;
      if (file.size > maxBytes) {
        setErrorMessage(
          isVi
            ? `Tệp "${file.name}" vượt quá kích thước cho phép (${maxSizeMb}MB).`
            : `File "${file.name}" exceeds the maximum allowed size (${maxSizeMb}MB).`
        );
        return;
      }

      const reader = new FileReader();
      reader.onload = () => {
        const dataUrl = reader.result as string;
        const newAttachment: AttachmentMetadata = {
          id: `att_${Date.now()}_${Math.random().toString(36).substring(2, 7)}`,
          name: file.name,
          size: file.size,
          type: file.type,
          dataUrl,
          uploadedAt: new Date().toISOString(),
        };
        onChange([...attachments, newAttachment]);
      };
      reader.onerror = () => {
        setErrorMessage(
          isVi ? `Không thể đọc tệp ${file.name}.` : `Unable to read file ${file.name}.`
        );
      };
      reader.readAsDataURL(file);
    });

    if (fileInputRef.current) {
      fileInputRef.current.value = '';
    }
  };

  const handleDragOver = (e: React.DragEvent) => {
    e.preventDefault();
    e.stopPropagation();
    if (!disabled) setIsDragging(true);
  };

  const handleDragLeave = (e: React.DragEvent) => {
    e.preventDefault();
    e.stopPropagation();
    setIsDragging(false);
  };

  const handleDrop = (e: React.DragEvent) => {
    e.preventDefault();
    e.stopPropagation();
    setIsDragging(false);
    if (disabled) return;
    processFiles(e.dataTransfer.files);
  };

  const handleRemove = (id: string) => {
    if (disabled) return;
    onChange(attachments.filter((item) => item.id !== id));
    setErrorMessage(null);
  };

  return (
    <div className="space-y-3">
      {/* Drop Zone */}
      <div
        onDragOver={handleDragOver}
        onDragLeave={handleDragLeave}
        onDrop={handleDrop}
        onClick={() => !disabled && fileInputRef.current?.click()}
        className={`relative border-2 border-dashed rounded-2xl p-5 text-center transition-all cursor-pointer select-none ${
          isDragging
            ? 'border-emerald-500 bg-emerald-50/60 dark:bg-emerald-950/20'
            : 'border-slate-200 dark:border-slate-700/80 bg-slate-50/50 dark:bg-slate-900/40 hover:bg-slate-50 dark:hover:bg-slate-800/60'
        } ${disabled ? 'opacity-60 cursor-not-allowed' : ''}`}
      >
        <input
          ref={fileInputRef}
          type="file"
          multiple
          accept="image/jpeg,image/png,image/webp,application/pdf"
          className="hidden"
          disabled={disabled}
          onChange={(e) => processFiles(e.target.files)}
        />

        <div className="flex flex-col items-center justify-center gap-2">
          <div className="p-2.5 rounded-xl bg-white dark:bg-slate-800 shadow-xs text-emerald-600 dark:text-emerald-400">
            <UploadCloud className="w-5 h-5" />
          </div>
          <div className="text-xs">
            <span className="font-bold text-slate-800 dark:text-slate-200">
              {isVi ? 'Nhấn để chọn tệp' : 'Click to select files'}
            </span>{' '}
            <span className="text-slate-500 dark:text-slate-400">
              {isVi ? 'hoặc kéo thả tệp vào đây' : 'or drag and drop files here'}
            </span>
          </div>
          <p className="text-[11px] text-slate-400 dark:text-slate-500">
            {isVi
              ? `Hỗ trợ hình ảnh JPG, PNG, WEBP hoặc tài liệu PDF (Tối đa ${maxSizeMb}MB mỗi tệp)`
              : `Supports JPG, PNG, WEBP images or PDF documents (Max ${maxSizeMb}MB per file)`}
          </p>
        </div>
      </div>

      {/* Error Notice */}
      {errorMessage && (
        <div className="flex items-center gap-2 p-2.5 rounded-xl bg-rose-50 dark:bg-rose-950/30 text-rose-600 dark:text-rose-400 text-xs font-semibold">
          <AlertCircle className="w-4 h-4 shrink-0" />
          <span>{errorMessage}</span>
        </div>
      )}

      {/* Selected Attachments List (Removable BEFORE submit) */}
      {attachments.length > 0 && (
        <div className="space-y-2">
          <div className="text-[11px] font-bold text-slate-500 uppercase tracking-wider">
            {isVi
              ? `Tệp đính kèm đã chọn (${attachments.length}/${maxFiles})`
              : `Selected attachments (${attachments.length}/${maxFiles})`}
          </div>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
            {attachments.map((att) => {
              const isPdf = att.type === 'application/pdf' || att.name.toLowerCase().endsWith('.pdf');
              return (
                <div
                  key={att.id}
                  className="flex items-center justify-between gap-2 p-2.5 rounded-xl bg-white dark:bg-slate-800/80 border border-slate-200 dark:border-slate-700/80 shadow-2xs group"
                >
                  <div className="flex items-center gap-2.5 min-w-0 flex-1">
                    {/* Thumbnail / Icon */}
                    {isPdf ? (
                      <div className="w-9 h-9 shrink-0 rounded-lg bg-rose-50 dark:bg-rose-950/40 border border-rose-200 dark:border-rose-900/50 flex items-center justify-center text-rose-600 dark:text-rose-400">
                        <FileText className="w-4 h-4" />
                      </div>
                    ) : (
                      <div className="w-9 h-9 shrink-0 rounded-lg overflow-hidden border border-slate-200 dark:border-slate-700 bg-slate-100 dark:bg-slate-800 flex items-center justify-center">
                        {att.dataUrl ? (
                          <img
                            src={att.dataUrl}
                            alt={att.name}
                            className="w-full h-full object-cover"
                          />
                        ) : (
                          <ImageIcon className="w-4 h-4 text-slate-400" />
                        )}
                      </div>
                    )}

                    <div className="min-w-0 flex-1">
                      <p className="text-xs font-bold text-slate-800 dark:text-slate-200 truncate" title={att.name}>
                        {att.name}
                      </p>
                      <p className="text-[10px] text-slate-400 font-mono">
                        {formatFileSize(att.size)} • {isPdf ? (isVi ? 'Tài liệu PDF' : 'PDF Document') : (isVi ? 'Hình ảnh' : 'Image')}
                      </p>
                    </div>
                  </div>

                  {!disabled && (
                    <button
                      type="button"
                      onClick={() => handleRemove(att.id)}
                      className="p-1 rounded-lg text-slate-400 hover:text-rose-600 hover:bg-rose-50 dark:hover:bg-rose-950/30 transition-colors cursor-pointer shrink-0"
                      title={isVi ? 'Xóa tệp đính kèm này' : 'Remove this attachment'}
                    >
                      <X className="w-4 h-4" />
                    </button>
                  )}
                </div>
              );
            })}
          </div>
        </div>
      )}
    </div>
  );
};
