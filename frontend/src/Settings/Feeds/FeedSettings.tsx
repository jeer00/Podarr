import React, { useCallback, useEffect, useState } from 'react';
import Alert from 'Components/Alert';
import FieldSet from 'Components/FieldSet';
import Form from 'Components/Form/Form';
import FormInput from 'Components/Form/FormInput';
import FormInputHelpText from 'Components/Form/FormInputHelpText';
import FormLabel from 'Components/Form/FormLabel';
import FormRow from 'Components/Form/FormRow';
import Button from 'Components/Link/Button';
import ClipboardButton from 'Components/Link/ClipboardButton';
import SpinnerButton from 'Components/Link/SpinnerButton';
import SpinnerErrorButton from 'Components/Link/SpinnerErrorButton';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import ConfirmModal from 'Components/Modal/ConfirmModal';
import PageContentBody from 'Components/Page/PageContentBody';
import PageHeading from 'Components/Page/PageHeading';
import { inputTypes, kinds } from 'Helpers/Props';
import {
  getFeedBaseUrl,
  getPodcastBucketFeedUrl,
  getPodcastFeedUrl,
  PodcastFeedSettings,
  usePodcastFeedSettings,
  usePublishPodcastFeeds,
  useResetPodcastFeedToken,
  useSavePodcastFeedSettings,
  useTestS3Settings,
} from 'Podcasts/usePodcastFeeds';
import { useSortedPodcasts } from 'Podcasts/usePodcasts';
import settingsStyles from 'Settings/Settings.module.css';
import SettingsPage from 'Settings/SettingsPage';
import { InputChanged } from 'typings/inputs';
import getErrorMessage from 'Utilities/Object/getErrorMessage';
import translate from 'Utilities/String/translate';
import styles from './FeedSettings.module.css';

type EditableSettings = Pick<
  PodcastFeedSettings,
  | 'enabled'
  | 'baseUrl'
  | 's3Enabled'
  | 's3Endpoint'
  | 's3Region'
  | 's3Bucket'
  | 's3AccessKey'
  | 's3SecretKey'
  | 's3Prefix'
  | 's3PublicUrl'
>;

const EDITABLE_KEYS: (keyof EditableSettings)[] = [
  'enabled',
  'baseUrl',
  's3Enabled',
  's3Endpoint',
  's3Region',
  's3Bucket',
  's3AccessKey',
  's3SecretKey',
  's3Prefix',
  's3PublicUrl',
];

function toEditable(settings: PodcastFeedSettings): EditableSettings {
  return {
    enabled: settings.enabled,
    baseUrl: settings.baseUrl ?? '',
    s3Enabled: settings.s3Enabled,
    s3Endpoint: settings.s3Endpoint ?? '',
    s3Region: settings.s3Region ?? '',
    s3Bucket: settings.s3Bucket ?? '',
    s3AccessKey: settings.s3AccessKey ?? '',
    s3SecretKey: '',
    s3Prefix: settings.s3Prefix ?? '',
    s3PublicUrl: settings.s3PublicUrl ?? '',
  };
}

function FeedUrl({ label, url }: { label: string; url: string }) {
  return (
    <div className={styles.feedRow}>
      <div className={styles.feedInfo}>
        <div className={styles.feedLabel}>{label}</div>
        <div className={styles.feedUrl}>{url}</div>
      </div>
      <ClipboardButton value={url} />
    </div>
  );
}

function FeedSettings() {
  const { data: settings, isFetched, error } = usePodcastFeedSettings();
  const { saveSettings, isSaving, saveError } = useSavePodcastFeedSettings();
  const { resetToken, isResetting } = useResetPodcastFeedToken();
  const { testS3, isTestingS3, testS3Error, testS3Succeeded, resetTestS3 } =
    useTestS3Settings();
  const { publishFeeds, isPublishing } = usePublishPodcastFeeds();
  const { data: podcasts } = useSortedPodcasts();

  const [values, setValues] = useState<EditableSettings | null>(null);
  const [isConfirmResetOpen, setIsConfirmResetOpen] = useState(false);

  useEffect(() => {
    if (settings) {
      setValues(toEditable(settings));
    }
  }, [settings]);

  const handleInputChange = useCallback(
    ({ name, value }: InputChanged<string | boolean>) => {
      setValues((current) =>
        current ? { ...current, [name]: value } : current
      );
      resetTestS3();
    },
    [resetTestS3]
  );

  const handleSavePress = useCallback(() => {
    if (settings && values) {
      saveSettings({ ...settings, ...values });
    }
  }, [settings, values, saveSettings]);

  const handleTestPress = useCallback(() => {
    if (settings && values) {
      testS3({ ...settings, ...values });
    }
  }, [settings, values, testS3]);

  const handlePublishPress = useCallback(() => {
    publishFeeds();
  }, [publishFeeds]);

  const handleResetPress = useCallback(() => {
    setIsConfirmResetOpen(true);
  }, []);

  const handleResetCancel = useCallback(() => {
    setIsConfirmResetOpen(false);
  }, []);

  const handleResetConfirm = useCallback(() => {
    setIsConfirmResetOpen(false);
    resetToken();
  }, [resetToken]);

  const hasChanges =
    !!settings &&
    !!values &&
    EDITABLE_KEYS.some((key) =>
      key === 's3SecretKey'
        ? !!values.s3SecretKey
        : (settings[key] ?? '') !== (values[key] ?? '')
    );

  const isS3Active = !!settings?.enabled && !!settings?.s3Enabled;
  const opmlUrl = settings?.s3PublicOpmlUrl;

  return (
    <SettingsPage title={translate('PodcastFeeds')} showSave={false}>
      <PageContentBody>
        <div className={settingsStyles.section}>
          <PageHeading
            scope={translate('Settings')}
            title={translate('PodcastFeeds')}
          />

          {!isFetched && !error ? <LoadingIndicator /> : null}

          {error ? (
            <Alert kind={kinds.DANGER}>{getErrorMessage(error)}</Alert>
          ) : null}

          {settings && values ? (
            <>
              <p className={styles.intro}>{translate('PodcastFeedsIntro')}</p>

              <Form>
                <FieldSet legend={translate('Settings')}>
                  <FormRow>
                    <FormLabel>{translate('ServePodcastFeeds')}</FormLabel>
                    <FormInput
                      type={inputTypes.CHECK}
                      name="enabled"
                      value={values.enabled}
                      helpText={translate('ServePodcastFeedsHelpText')}
                      onChange={handleInputChange}
                    />
                  </FormRow>

                  <FormRow>
                    <FormLabel>{translate('FeedBaseUrl')}</FormLabel>
                    <FormInputHelpText
                      text={translate('FeedBaseUrlHelpText')}
                    />
                    <FormInput
                      type={inputTypes.TEXT}
                      name="baseUrl"
                      value={values.baseUrl ?? ''}
                      placeholder={settings.feedBaseUrl.replace(
                        /\/feed\/.*$/,
                        ''
                      )}
                      onChange={handleInputChange}
                    />
                  </FormRow>
                </FieldSet>

                <FieldSet legend={translate('UploadFeedsToS3')}>
                  <p className={styles.intro}>
                    {translate('UploadFeedsToS3Intro')}
                  </p>

                  <FormRow>
                    <FormLabel>{translate('UploadFeedsToS3')}</FormLabel>
                    <FormInput
                      type={inputTypes.CHECK}
                      name="s3Enabled"
                      value={values.s3Enabled}
                      helpText={translate('UploadFeedsToS3HelpText')}
                      onChange={handleInputChange}
                    />
                  </FormRow>

                  <FormRow>
                    <FormLabel>{translate('S3Endpoint')}</FormLabel>
                    <FormInputHelpText text={translate('S3EndpointHelpText')} />
                    <FormInput
                      type={inputTypes.TEXT}
                      name="s3Endpoint"
                      value={values.s3Endpoint ?? ''}
                      placeholder="https://<account id>.r2.cloudflarestorage.com"
                      onChange={handleInputChange}
                    />
                  </FormRow>

                  <FormRow>
                    <FormLabel>{translate('S3Bucket')}</FormLabel>
                    <FormInput
                      type={inputTypes.TEXT}
                      name="s3Bucket"
                      value={values.s3Bucket ?? ''}
                      onChange={handleInputChange}
                    />
                  </FormRow>

                  <FormRow>
                    <FormLabel>{translate('S3AccessKey')}</FormLabel>
                    <FormInput
                      type={inputTypes.TEXT}
                      name="s3AccessKey"
                      value={values.s3AccessKey ?? ''}
                      onChange={handleInputChange}
                    />
                  </FormRow>

                  <FormRow>
                    <FormLabel>{translate('S3SecretKey')}</FormLabel>
                    <FormInput
                      type={inputTypes.PASSWORD}
                      name="s3SecretKey"
                      value={values.s3SecretKey ?? ''}
                      placeholder={
                        settings.hasS3SecretKey
                          ? translate('S3SecretKeySaved')
                          : undefined
                      }
                      onChange={handleInputChange}
                    />
                  </FormRow>

                  <FormRow>
                    <FormLabel>{translate('S3Region')}</FormLabel>
                    <FormInputHelpText text={translate('S3RegionHelpText')} />
                    <FormInput
                      type={inputTypes.TEXT}
                      name="s3Region"
                      value={values.s3Region ?? ''}
                      placeholder="auto"
                      onChange={handleInputChange}
                    />
                  </FormRow>

                  <FormRow>
                    <FormLabel>{translate('S3Prefix')}</FormLabel>
                    <FormInputHelpText text={translate('S3PrefixHelpText')} />
                    <FormInput
                      type={inputTypes.TEXT}
                      name="s3Prefix"
                      value={values.s3Prefix ?? ''}
                      placeholder="podarr"
                      onChange={handleInputChange}
                    />
                  </FormRow>

                  <FormRow>
                    <FormLabel>{translate('S3PublicUrl')}</FormLabel>
                    <FormInputHelpText
                      text={translate('S3PublicUrlHelpText')}
                    />
                    <FormInput
                      type={inputTypes.TEXT}
                      name="s3PublicUrl"
                      value={values.s3PublicUrl ?? ''}
                      placeholder="https://pub-<id>.r2.dev"
                      onChange={handleInputChange}
                    />
                  </FormRow>

                  {testS3Error ? (
                    <Alert kind={kinds.DANGER}>
                      {getErrorMessage(testS3Error)}
                    </Alert>
                  ) : null}

                  {testS3Succeeded ? (
                    <Alert kind={kinds.SUCCESS}>
                      {translate('S3TestSucceeded')}
                    </Alert>
                  ) : null}
                </FieldSet>
              </Form>

              <div className={styles.actions}>
                <SpinnerErrorButton
                  kind={kinds.PRIMARY}
                  isSpinning={isSaving}
                  isDisabled={!hasChanges}
                  error={saveError}
                  onPress={handleSavePress}
                >
                  {translate('Save')}
                </SpinnerErrorButton>

                <SpinnerButton
                  isSpinning={isTestingS3}
                  onPress={handleTestPress}
                >
                  {translate('TestS3Upload')}
                </SpinnerButton>

                <SpinnerButton
                  isSpinning={isPublishing}
                  isDisabled={!isS3Active}
                  onPress={handlePublishPress}
                >
                  {translate('UploadFeedsNow')}
                </SpinnerButton>

                <Button
                  kind={kinds.DANGER}
                  isDisabled={isResetting}
                  onPress={handleResetPress}
                >
                  {translate('ResetFeedLinks')}
                </Button>
              </div>

              {settings.enabled ? (
                <FieldSet legend={translate('FeedLinks')}>
                  <Alert kind={kinds.INFO}>
                    {translate('FeedLinksPrivateWarning')}
                  </Alert>

                  {isS3Active && opmlUrl ? (
                    <FeedUrl
                      label={translate('AllPodcastsOpmlBucket')}
                      url={opmlUrl}
                    />
                  ) : (
                    <FeedUrl
                      label={translate('AllPodcastsOpml')}
                      url={`${getFeedBaseUrl(settings)}/podcasts.opml`}
                    />
                  )}

                  {podcasts.map((podcast) => {
                    const bucketUrl = isS3Active
                      ? getPodcastBucketFeedUrl(settings, podcast.id)
                      : undefined;

                    return (
                      <FeedUrl
                        key={podcast.id}
                        label={
                          bucketUrl
                            ? `${podcast.title} (${translate('Bucket')})`
                            : podcast.title
                        }
                        url={
                          bucketUrl ?? getPodcastFeedUrl(settings, podcast.id)
                        }
                      />
                    );
                  })}
                </FieldSet>
              ) : null}

              <ConfirmModal
                isOpen={isConfirmResetOpen}
                kind={kinds.DANGER}
                title={translate('ResetFeedLinks')}
                message={translate('ResetFeedLinksMessage')}
                confirmLabel={translate('Reset')}
                onConfirm={handleResetConfirm}
                onCancel={handleResetCancel}
              />
            </>
          ) : null}
        </div>
      </PageContentBody>
    </SettingsPage>
  );
}

export default FeedSettings;
